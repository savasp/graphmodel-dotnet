#!/usr/bin/env ruby
# Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
# See LICENSE in the project root for full license terms.

require_relative "../eng/docs/documentation_validator"

root = File.expand_path("..", __dir__)
markdown_files = CvoyaDocumentation::MarkdownFiles.discover(root)
# DocFX owns its generated api/ links and already treats its own warnings as
# errors. The source validator covers every other maintained Markdown surface.
source_link_files = markdown_files.reject do |path|
  path.start_with?(File.join(root, "docs", "apiref") + File::SEPARATOR)
end

link_errors = CvoyaDocumentation::MarkdownLinkValidator.new(root).validate(source_link_files)
snippet_errors = CvoyaDocumentation::CheckedSnippetValidator.new(root).validate(markdown_files)
errors = link_errors + snippet_errors

unless errors.empty?
  warn errors.join("\n")
  warn "Documentation source validation failed with #{errors.length} error(s)."
  exit 1
end

puts "Validated #{markdown_files.length} Markdown files, local links, anchors, and checked snippets."
