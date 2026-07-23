# Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
# See LICENSE in the project root for full license terms.

require "cgi"
require "pathname"
require "set"
require "uri"

module CvoyaDocumentation
  class MarkdownFiles
    PATTERNS = [
      "*.md",
      ".github/**/*.md",
      "developer/**/*.md",
      "docs/**/*.md",
      "examples/**/*.md",
      "scripts/**/*.md",
      "src/**/*.md",
      "tests/**/*.md"
    ].freeze

    def self.discover(root)
      PATTERNS
        .flat_map { |pattern| Dir.glob(File.join(root, pattern), File::FNM_DOTMATCH) }
        .select { |path| File.file?(path) }
        .reject { |path| path.split(File::SEPARATOR).any? { |part| part == "bin" || part == "obj" } }
        .uniq
        .sort
    end
  end

  class MarkdownLinkValidator
    INLINE_LINK = /!?\[[^\]\n]*\]\(\s*(?<target><[^>\n]+>|(?:\\[^\n]|[^\\)\s\n])+)(?:\s+(?:"[^"\n]*"|'[^'\n]*'|\([^)\n]*\)))?\s*\)/.freeze
    REFERENCE_LINK = /^\s{0,3}\[[^\]\n]+\]:\s*(?<target><[^>\n]+>|\S+)/.freeze
    HTML_LINK = /<(?:a|img|source)\b[^>]*\b(?:href|src|srcset)=["'](?<target>[^"']+)["'][^>]*>/i.freeze
    EXPLICIT_ANCHOR = /<[^>]+\b(?:id|name)=["'](?<anchor>[^"']+)["'][^>]*>/i.freeze
    ATX_HEADING = /^\s{0,3}[#]{1,6}\s+(?<heading>.+?)\s*#*\s*$/.freeze
    SETEXT_HEADING = /^\s{0,3}(?:=+|-+)\s*$/.freeze
    URI_SCHEME = /\A[a-z][a-z0-9+.-]*:/i.freeze

    def initialize(root)
      @root = File.expand_path(root)
      @anchor_cache = {}
    end

    def validate(files)
      files.flat_map { |path| validate_file(path) }
    end

    private

    def validate_file(path)
      text = File.read(path, encoding: Encoding::UTF_8)
      searchable = without_code(text)
      links = []

      searchable.to_enum(:scan, INLINE_LINK).each do
        match = Regexp.last_match
        links << [line_number(searchable, match.begin(0)), match[:target]]
      end

      searchable.each_line.with_index(1) do |line, number|
        if (match = line.match(REFERENCE_LINK))
          links << [number, match[:target]]
        end
      end

      searchable.to_enum(:scan, HTML_LINK).each do
        match = Regexp.last_match
        links << [line_number(searchable, match.begin(0)), match[:target]]
      end

      links
        .uniq
        .map { |number, target| validate_target(path, number, target) }
        .compact
    end

    def without_code(text)
      in_fence = false
      fence_character = nil
      fence_length = 0

      text.each_line.map do |line|
        if in_fence
          if line.match?(/^\s{0,3}#{Regexp.escape(fence_character)}{#{fence_length},}\s*$/)
            in_fence = false
          end
          "\n"
        elsif (match = line.match(/^\s{0,3}(?<fence>`{3,}|~{3,})/))
          in_fence = true
          fence_character = match[:fence][0]
          fence_length = match[:fence].length
          "\n"
        else
          line.gsub(/`[^`\n]*`/, "")
        end
      end.join
    end

    def validate_target(source, line, raw_target)
      target = CGI.unescapeHTML(raw_target.strip)
      target = target[1...-1] if target.start_with?("<") && target.end_with?(">")
      target = target.gsub("\\ ", " ")

      return if target.empty? ||
        target.start_with?("//") ||
        target.include?("{{") ||
        target.include?("{%") ||
        target.match?(URI_SCHEME)

      path_part, fragment = target.split("#", 2)
      path_part = path_part.split("?", 2).first
      path_part = uri_unescape(path_part)
      fragment = uri_unescape(fragment) unless fragment.nil?

      destination = if path_part.empty?
        source
      elsif path_part.start_with?("/")
        File.join(@root, path_part.delete_prefix("/"))
      else
        File.expand_path(path_part, File.dirname(source))
      end

      unless inside_root?(destination)
        return error(source, line, target, "target resolves outside the repository")
      end

      if File.directory?(destination)
        return if fragment.nil? || fragment.empty?
        destination = File.join(destination, "README.md")
      end
      unless File.file?(destination)
        return error(
          source,
          line,
          target,
          "target '#{relative(destination)}' does not exist"
        )
      end

      return if fragment.nil? || fragment.empty? || File.extname(destination).downcase != ".md"

      anchor = fragment.delete_prefix("#")
      return if anchors_for(destination).include?(anchor)

      error(
        source,
        line,
        target,
        "anchor '##{anchor}' was not found in '#{relative(destination)}'"
      )
    end

    def anchors_for(path)
      @anchor_cache[path] ||= begin
        text = File.read(path, encoding: Encoding::UTF_8)
        searchable = without_code(text)
        lines = searchable.lines
        anchors = Set.new
        slug_counts = Hash.new(0)

        searchable.to_enum(:scan, EXPLICIT_ANCHOR).each do
          anchors << Regexp.last_match[:anchor]
        end

        lines.each_with_index do |line, index|
          heading = if (match = line.match(ATX_HEADING))
            match[:heading]
          elsif index + 1 < lines.length && lines[index + 1].match?(SETEXT_HEADING)
            line.strip
          end
          next if heading.nil? || heading.empty?

          base_slug = github_slug(heading)
          next if base_slug.empty?

          occurrence = slug_counts[base_slug]
          slug_counts[base_slug] += 1
          anchors << (occurrence.zero? ? base_slug : "#{base_slug}-#{occurrence}")
        end

        anchors
      end
    end

    def github_slug(heading)
      value = text_without_html_tags(heading)
      value.gsub!(/!\[([^\]]*)\]\([^)]+\)/, '\1')
      value.gsub!(/\[([^\]]+)\]\([^)]+\)/, '\1')
      value.gsub!(/[`*_~]/, "")
      value.downcase!
      value.gsub!(/[^\p{L}\p{N}\s_-]/u, "")
      value.strip.gsub(/\s+/, "-")
    end

    def text_without_html_tags(value)
      text = +""
      tag = nil
      quote = nil

      value.each_char do |character|
        if tag.nil?
          if character == "<"
            tag = +"<"
          else
            text << character
          end
          next
        end

        tag << character
        if quote
          quote = nil if character == quote
        elsif character == '"' || character == "'"
          quote = character
        elsif character == ">"
          tag = nil
        end
      end

      text << tag unless tag.nil?
      text
    end

    def uri_unescape(value)
      URI::DEFAULT_PARSER.unescape(value.to_s)
    rescue ArgumentError
      value.to_s
    end

    def inside_root?(path)
      expanded = File.expand_path(path)
      expanded == @root || expanded.start_with?("#{@root}#{File::SEPARATOR}")
    end

    def line_number(text, offset)
      text[0...offset].count("\n") + 1
    end

    def error(source, line, target, reason)
      "#{relative(source)}:#{line}: broken local link '#{target}': #{reason}"
    end

    def relative(path)
      Pathname.new(File.expand_path(path)).relative_path_from(Pathname.new(@root)).to_s
    rescue ArgumentError
      path
    end
  end

  class CheckedSnippetValidator
    ANNOTATION = /^\s*<!--\s*checked-snippet:\s*(?<sources>.+?)\s*-->\s*$/.freeze
    OPENING_FENCE = /^\s{0,3}(?<fence>`{3,}|~{3,})\s*(?<language>csharp|cs)\s*$/.freeze

    def initialize(root)
      @root = File.expand_path(root)
      @source_cache = {}
    end

    def validate(files)
      files.flat_map { |path| validate_file(path) }
    end

    private

    def validate_file(path)
      lines = File.read(path, encoding: Encoding::UTF_8).lines(chomp: true)
      errors = []
      index = 0

      while index < lines.length
        annotation = lines[index].match(ANNOTATION)
        unless annotation
          index += 1
          next
        end

        annotation_line = index + 1
        index += 1
        index += 1 while index < lines.length && lines[index].strip.empty?
        opening = index < lines.length ? lines[index].match(OPENING_FENCE) : nil
        unless opening
          errors << "#{relative(path)}:#{annotation_line}: checked-snippet must be followed by a csharp code fence"
          next
        end

        fence_character = opening[:fence][0]
        fence_length = opening[:fence].length
        code_line = index + 2
        index += 1
        code = []
        while index < lines.length &&
          !lines[index].match?(/^\s{0,3}#{Regexp.escape(fence_character)}{#{fence_length},}\s*$/)
          code << lines[index]
          index += 1
        end

        if index >= lines.length
          errors << "#{relative(path)}:#{annotation_line}: checked-snippet code fence is not closed"
          next
        end

        expected, source_errors = expected_source(annotation[:sources])
        errors.concat(source_errors.map { |message| "#{relative(path)}:#{annotation_line}: #{message}" })
        if source_errors.empty?
          actual = code.join("\n")
          unless actual == expected
            differing_line = first_difference(actual, expected)
            errors << [
              "#{relative(path)}:#{code_line + differing_line}: checked snippet differs from its compiled source",
              "(#{annotation[:sources]}); update the source region and copy it here"
            ].join(" ")
          end
        end

        index += 1
      end

      errors
    end

    def expected_source(source_list)
      errors = []
      regions = source_list.split(";").map(&:strip).map do |source_spec|
        source_path, region_name = source_spec.split("#", 2)
        if source_path.nil? || source_path.empty? || region_name.nil? || region_name.empty?
          errors << "invalid checked-snippet source '#{source_spec}' (expected path#region)"
          next
        end

        unless source_path.start_with?("examples/")
          errors << "checked-snippet source '#{source_path}' must live under examples/"
          next
        end

        absolute_path = File.expand_path(source_path, @root)
        unless inside_root?(absolute_path) && File.file?(absolute_path)
          errors << "checked-snippet source '#{source_path}' does not exist"
          next
        end

        unless example_project_ancestor?(absolute_path)
          errors << "checked-snippet source '#{source_path}' is not owned by an example project"
          next
        end

        region, region_error = source_region(absolute_path, region_name)
        errors << region_error unless region_error.nil?
        region
      end.compact

      [regions.join("\n\n"), errors]
    end

    def source_region(path, name)
      lines = (@source_cache[path] ||= File.read(path, encoding: Encoding::UTF_8).lines(chomp: true))
      start_indexes = lines.each_index.select do |index|
        lines[index].match?(/^\s*\/\/\s*snippet-start:\s*#{Regexp.escape(name)}\s*$/)
      end

      if start_indexes.length != 1
        return [nil, "compiled source '#{relative(path)}' must contain exactly one snippet-start: #{name} marker"]
      end

      start_index = start_indexes.first
      end_indexes = ((start_index + 1)...lines.length).select do |index|
        lines[index].match?(/^\s*\/\/\s*snippet-end:\s*#{Regexp.escape(name)}\s*$/)
      end
      if end_indexes.empty?
        return [nil, "compiled source '#{relative(path)}' is missing snippet-end: #{name}"]
      end

      region = lines[(start_index + 1)...end_indexes.first]
      [dedent(region).join("\n"), nil]
    end

    def dedent(lines)
      indentation = lines
        .reject { |line| line.strip.empty? }
        .map { |line| line[/\A[ \t]*/].length }
        .min || 0
      lines.map { |line| line.strip.empty? ? "" : line[indentation..-1] }
    end

    def example_project_ancestor?(path)
      directory = File.dirname(path)
      examples_root = File.join(@root, "examples")
      while directory.start_with?("#{examples_root}#{File::SEPARATOR}") || directory == examples_root
        return true unless Dir.glob(File.join(directory, "*.csproj")).empty?
        directory = File.dirname(directory)
      end
      false
    end

    def first_difference(actual, expected)
      actual_lines = actual.split("\n", -1)
      expected_lines = expected.split("\n", -1)
      limit = [actual_lines.length, expected_lines.length].max
      (0...limit).find { |index| actual_lines[index] != expected_lines[index] } || 0
    end

    def inside_root?(path)
      expanded = File.expand_path(path)
      expanded == @root || expanded.start_with?("#{@root}#{File::SEPARATOR}")
    end

    def relative(path)
      Pathname.new(File.expand_path(path)).relative_path_from(Pathname.new(@root)).to_s
    rescue ArgumentError
      path
    end
  end
end
