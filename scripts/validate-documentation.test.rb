#!/usr/bin/env ruby
# Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
# See LICENSE in the project root for full license terms.

require "fileutils"
require "minitest/autorun"
require "tmpdir"
require_relative "../eng/docs/documentation_validator"

class DocumentationValidatorTest < Minitest::Test
  def setup
    @root = Dir.mktmpdir("cvoya-documentation-validator")
  end

  def teardown
    FileUtils.remove_entry(@root)
  end

  def test_local_files_directories_and_duplicate_heading_anchors_are_valid
    write("README.md", <<~MARKDOWN)
      # Project

      [Guide](docs/guide.md#repeated-heading-1)
      [Decisions](docs/decisions/#accepted)
    MARKDOWN
    write("docs/guide.md", <<~MARKDOWN)
      # Repeated heading

      ## Repeated heading
    MARKDOWN
    write("docs/decisions/README.md", <<~MARKDOWN)
      # Decisions

      ## Accepted
    MARKDOWN

    errors = link_validator.validate(markdown_files)

    assert_empty errors
  end

  def test_broken_relative_link_and_anchor_report_markdown_file_and_line
    write("README.md", <<~MARKDOWN)
      # Project

      [Missing file](docs/missing.md)
      [Missing anchor](docs/guide.md#not-there)
    MARKDOWN
    write("docs/guide.md", "# Guide\n")

    errors = link_validator.validate(markdown_files)

    assert errors.any? { |error| error.include?("README.md:3:") && error.include?("does not exist") }
    assert errors.any? { |error| error.include?("README.md:4:") && error.include?("anchor '#not-there'") }
  end

  def test_checked_snippet_matches_compiled_example_region
    write("examples/Sample/Sample.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\" />\n")
    write("examples/Sample/Snippets.cs", <<~CSHARP)
      internal static class Snippets
      {
          // snippet-start: query
          private static void Query(IGraph graph)
          {
              var people = graph.Nodes<Person>();
          }
          // snippet-end: query
      }
    CSHARP
    write("README.md", <<~MARKDOWN)
      # Project

      <!-- checked-snippet: examples/Sample/Snippets.cs#query -->
      ```csharp
      private static void Query(IGraph graph)
      {
          var people = graph.Nodes<Person>();
      }
      ```
    MARKDOWN

    errors = snippet_validator.validate(markdown_files)

    assert_empty errors
  end

  def test_removed_api_in_checked_snippet_reports_markdown_file_and_line
    write("examples/Sample/Sample.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\" />\n")
    write("examples/Sample/Snippets.cs", <<~CSHARP)
      internal static class Snippets
      {
          // snippet-start: query
          private static void Query(IGraph graph)
          {
              var people = graph.Nodes<Person>();
          }
          // snippet-end: query
      }
    CSHARP
    write("README.md", <<~MARKDOWN)
      # Project

      <!-- checked-snippet: examples/Sample/Snippets.cs#query -->
      ```csharp
      private static void Query(IGraph graph)
      {
          var people = graph.NodesAsync<Person>();
      }
      ```
    MARKDOWN

    errors = snippet_validator.validate(markdown_files)

    assert errors.any? do |error|
      error.include?("README.md:7:") &&
        error.include?("differs from its compiled source")
    end
  end

  private

  def write(relative_path, content)
    path = File.join(@root, relative_path)
    FileUtils.mkdir_p(File.dirname(path))
    File.write(path, content)
  end

  def markdown_files
    CvoyaDocumentation::MarkdownFiles.discover(@root)
  end

  def link_validator
    CvoyaDocumentation::MarkdownLinkValidator.new(@root)
  end

  def snippet_validator
    CvoyaDocumentation::CheckedSnippetValidator.new(@root)
  end
end
