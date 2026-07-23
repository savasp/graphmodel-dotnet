# Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
# See LICENSE in the project root for full license terms.

require "yaml"

ROOT = File.expand_path("..", __dir__)
DOCS_WORKFLOW_PATH = File.join(ROOT, ".github/workflows/docs.yml")
RELEASE_WORKFLOW_PATH = File.join(ROOT, ".github/workflows/release.yml")

def check(condition, message)
  raise message unless condition
end

# Workflow files contain UTF-8 step names, so never depend on the ambient locale.
def read_workflow_source(path)
  File.read(path, encoding: Encoding::UTF_8)
end

def load_workflow(path)
  YAML.safe_load(read_workflow_source(path), aliases: true)
end

def step_named(job, name)
  step = job.fetch("steps").find { |candidate| candidate["name"] == name }
  raise "workflow step named #{name.inspect} is missing" unless step
  step
end

def path_selected?(patterns, path)
  flags = File::FNM_EXTGLOB
  patterns.any? { |pattern| File.fnmatch?(pattern, path, flags) }
end

docs = load_workflow(DOCS_WORKFLOW_PATH)
docs_source = read_workflow_source(DOCS_WORKFLOW_PATH)
docs_triggers = docs.fetch(true)
docs_job = docs.fetch("jobs").fetch("validate-documentation")

check(docs_triggers.key?("pull_request"), "documentation validation must run on pull requests")
check(docs_triggers.key?("push"), "documentation validation must run on main-branch pushes")
check(docs_triggers.key?("workflow_call"), "the release must reuse the validation workflow")
check(docs.fetch("permissions") == { "contents" => "read" }, "documentation validation must remain read-only")
check(!docs_source.include?("actions/deploy-pages@"), "the validation workflow must not contain a Pages deployment action")
check(!docs_source.match?(/^\s+(pages|id-token):\s+write\s*$/), "the validation workflow must not request deployment permissions")

push_paths = docs_triggers.fetch("push").fetch("paths")
pull_request_paths = docs_triggers.fetch("pull_request").fetch("paths")
check(push_paths == pull_request_paths, "push and pull-request documentation paths must stay identical")

maintained_documentation_paths = [
  "CONTRIBUTING.md",
  "SECURITY.md",
  "assets/future-diagram.svg",
  "developer/release-notes/v1.0.0.md",
  "eng/docs/future-validator.rb",
  "examples/FutureExample/Program.cs",
  "examples/FutureExample/README.md",
  "scripts/README.md",
  "scripts/validate-documentation.rb",
  "tests/Future.Tests/README.md",
  ".github/ISSUE_TEMPLATE/future.yml",
  ".github/pull_request_template.md"
]
maintained_documentation_paths.each do |path|
  check(path_selected?(push_paths, path), "documentation workflow does not cover #{path}")
end

check(step_named(docs_job, "📖 Build API Reference Site").fetch("run").include?("--warningsAsErrors"), "DocFX warnings must fail validation")
check(
  step_named(docs_job, "✅ Validate Documentation Sources and Examples").fetch("run").include?("validate-documentation.sh"),
  "pull requests must validate source links, checked snippets, and compiling examples"
)
check(step_named(docs_job, "🔗 Validate Internal Links").fetch("run").include?("htmlproofer"), "pull requests must validate internal links")
check(step_named(docs_job, "Verify Tagged Source").fetch("run").include?('"v$RELEASE_VERSION"'), "documentation must verify that tag and package version agree")
check(step_named(docs_job, "Record Release Provenance").fetch("run").include?("_site/release.json"), "release provenance must be part of the generated site")
check(step_named(docs_job, "Prepare Documentation Build").fetch("run").include?("release_time"), "release builds must pin Jekyll's generated time to the tagged commit")
check(step_named(docs_job, "Normalize Release Artifact Timestamps").fetch("run").include?("SOURCE_DATE_EPOCH"), "release artifacts must normalize file timestamps for retry safety")

artifact_name = step_named(docs_job, "Name Pages Artifact").fetch("run")
check(artifact_name.include?("GITHUB_RUN_ID") && artifact_name.include?("GITHUB_RUN_ATTEMPT"), "release retries must use an immutable artifact name per attempt")

release = load_workflow(RELEASE_WORKFLOW_PATH)
release_jobs = release.fetch("jobs")
build_docs = release_jobs.fetch("build-docs")
deploy_docs = release_jobs.fetch("deploy-docs")
github_release = release_jobs.fetch("github-release")

check(build_docs.fetch("uses") == "./.github/workflows/docs.yml", "the release must use the same documentation build as pull requests")
check(Array(build_docs.fetch("needs")).include?("pack"), "release documentation must wait for successful package artifacts")
check(build_docs.fetch("if").include?("github.event_name == 'push'"), "manual release dry runs must not produce deployable documentation")
check(build_docs.fetch("with").fetch("release_commit") == "${{ github.sha }}", "documentation must use the release commit")
check(build_docs.fetch("with").fetch("release_version").include?("resolve-version"), "documentation must use the tag-derived package version")

deploy_needs = Array(deploy_docs.fetch("needs"))
check(deploy_needs.include?("build-docs") && deploy_needs.include?("github-release"), "deployment must wait for documentation and the completed release")
check(deploy_docs.fetch("if").include?("github.event_name == 'push'"), "manual release dry runs must not deploy")
check(deploy_docs.fetch("environment").fetch("name") == "github-pages", "deployment must use the protected Pages environment")
check(deploy_docs.fetch("permissions") == { "contents" => "read", "pages" => "write", "id-token" => "write" }, "deployment permissions must be scoped to the deployment job")
check(step_named(deploy_docs, "Deploy to GitHub Pages").fetch("uses").start_with?("actions/deploy-pages@"), "only the release workflow may deploy Pages")

release_creation = step_named(github_release, "Create GitHub Release").fetch("run")
check(
  release_creation.include?('developer/release-notes/${GITHUB_REF_NAME}.md'),
  "the release workflow must prefer checked-in version-specific release notes"
)

v1_release_notes = File.read(File.join(ROOT, "developer/release-notes/v1.0.0.md"), encoding: Encoding::UTF_8)
check(v1_release_notes.include?("first stable release"), "v1.0.0 notes must introduce the stable release")
check(v1_release_notes.include?("recreate and reimport"), "v1.0.0 notes must state the pre-v1 data boundary")
check(v1_release_notes.include?("shortest path"), "v1.0.0 notes must state the AGE capability difference")
check(v1_release_notes.include?("@paule96"), "v1.0.0 notes must credit the AGE contribution")

puts "Documentation deployment workflow policy passed."
