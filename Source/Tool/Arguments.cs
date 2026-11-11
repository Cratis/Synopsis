// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

sealed class Arguments
{
    static readonly Dictionary<string, Action<Arguments, string>> ValueOptions = new(StringComparer.Ordinal)
    {
        ["-o"] = (arguments, value) => arguments.Output = value,
        ["--output"] = (arguments, value) => arguments.Output = value,
        ["-f"] = (arguments, value) => arguments.Format = value.ToLowerInvariant(),
        ["--format"] = (arguments, value) => arguments.Format = value.ToLowerInvariant(),
        ["--title"] = (arguments, value) => arguments.Title = value,
        ["--description"] = (arguments, value) => arguments.Description = value,
        ["--source-url"] = (arguments, value) => arguments.SourceUrl = value,
        ["--config"] = (arguments, value) => arguments.Configuration = value,
        ["--skip-segments"] = (arguments, value) => arguments.SkipSegments.AddRange(Split(value)),
        ["--exclude"] = (arguments, value) => arguments.Exclude.AddRange(Split(value))
    };

    static readonly Dictionary<string, Action<Arguments>> Flags = new(StringComparer.Ordinal)
    {
        ["--fail-on-empty"] = arguments => arguments.FailOnEmpty = true,
        ["--open"] = arguments => arguments.Open = true,
        ["--quiet"] = arguments => arguments.Quiet = true
    };

    bool _hasInput;

    public string Input { get; private set; } = ".";
    public string Output { get; private set; } = "synopsis.html";
    public string Format { get; private set; } = "html";
    public string? Title { get; private set; }
    public string? Description { get; private set; }
    public string? SourceUrl { get; private set; }
    public string? Configuration { get; private set; }
    public List<string> SkipSegments { get; } = [];
    public List<string> Exclude { get; } = [];
    public bool FailOnEmpty { get; private set; }
    public bool Open { get; private set; }
    public bool Quiet { get; private set; }

    public static Arguments Parse(string[] values)
    {
        var arguments = new Arguments();
        for (var index = 0; index < values.Length; index++)
        {
            var value = values[index];
            if (ValueOptions.TryGetValue(value, out var apply))
            {
                apply(arguments, ValueAfter(values, ++index, value));
            }
            else if (Flags.TryGetValue(value, out var flag))
            {
                flag(arguments);
            }
            else
            {
                arguments.SetInput(value);
            }
        }
        return arguments;
    }

    static string ValueAfter(string[] values, int index, string option) =>
        index < values.Length ? values[index] : throw new CliError($"Missing value after '{option}'.");

    static string[] Split(string value) => value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    void SetInput(string value)
    {
        if (value.StartsWith('-'))
        {
            throw new CliError($"Unknown option '{value}'.");
        }
        if (_hasInput)
        {
            throw new CliError("Only one input path can be supplied.");
        }
        Input = value;
        _hasInput = true;
    }
}
