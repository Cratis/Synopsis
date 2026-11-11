// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Synopsis.Discovery;
using Cratis.Synopsis.Model;

namespace Cratis.Synopsis.Parsing;

internal partial class GherkinSpecificationParser
{
    /// <summary>
    /// Reads a feature file line by line, tracking the current feature, rule, background and scenario.
    /// </summary>
    sealed class FeatureReader(SourceFile file, DiscoveryContext context, List<BehaviorScenario> result)
    {
        readonly Dictionary<string, List<Step>> _backgrounds = new(StringComparer.Ordinal);
        string _feature = Path.GetFileNameWithoutExtension(file.RelativePath);
        string? _rule;
        Scenario? _scenario;
        List<Step>? _background;
        Phase _phase = Phase.Given;
        bool _inExamples;
        IReadOnlyList<string>? _exampleHeader;

        public void Read(int index, string text)
        {
            if (IsIgnorable(text))
            {
                return;
            }

            if (TryReadDeclaration(index, text) || TryReadExampleRow(text))
            {
                return;
            }
            ReadStep(text);
        }

        static bool IsIgnorable(string text) => text.Length == 0 || text[0] is '#' or '@';

        static bool IsTableRow(string text) => text.StartsWith('|') && text.EndsWith('|');

        public void FinishScenario()
        {
            if (_scenario is null)
            {
                return;
            }

            var background = _backgrounds.GetValueOrDefault(_scenario.Rule ?? string.Empty) ?? _backgrounds.GetValueOrDefault(string.Empty) ?? [];
            result.AddRange(BuildScenarios(new(file, context, _feature), _scenario, background));
            _scenario = null;
            _inExamples = false;
            _exampleHeader = null;
        }

        bool TryReadDeclaration(int index, string text)
        {
            if (TryDeclaration(text, "Feature", out var value))
            {
                FinishScenario();
                _feature = value;
                _rule = null;
            }
            else if (TryDeclaration(text, "Rule", out value))
            {
                FinishScenario();
                _rule = value;
            }
            else if (TryDeclaration(text, "Background", out _))
            {
                StartBackground();
            }
            else if (TryScenario(text, out value, out var outline))
            {
                StartScenario(new(value, index + 1, _rule, outline));
            }
            else if (TryDeclaration(text, "Examples", out _))
            {
                StartExamples();
            }
            else
            {
                return false;
            }
            return true;
        }

        void StartBackground()
        {
            FinishScenario();
            _background = [];
            _backgrounds[_rule ?? string.Empty] = _background;
            _phase = Phase.Given;
        }

        void StartScenario(Scenario scenario)
        {
            FinishScenario();
            _background = null;
            _scenario = scenario;
            _phase = Phase.Given;
        }

        void StartExamples()
        {
            if (_scenario is not null)
            {
                _inExamples = true;
                _exampleHeader = null;
            }
        }

        bool IsExampleRow(string text) => _inExamples && _scenario is not null && IsTableRow(text);

        bool TryReadExampleRow(string text)
        {
            if (!IsExampleRow(text))
            {
                return false;
            }

            var cells = text[1..^1].Split('|').Select(_ => _.Trim()).ToList();
            if (_exampleHeader is null)
            {
                _exampleHeader = cells;
            }
            else if (cells.Count == _exampleHeader.Count)
            {
                _scenario!.Examples.Add(_exampleHeader.Zip(cells).ToDictionary(_ => _.First, _ => _.Second, StringComparer.Ordinal));
            }
            return true;
        }

        void ReadStep(string text)
        {
            if (StepKeywordOf(text) is not { } keyword)
            {
                return;
            }

            _phase = PhaseOf(keyword, _phase);
            var step = new Step(_phase, text[(keyword.Length + 1)..].Trim());
            (_scenario?.Steps ?? _background)?.Add(step);
        }
    }
}
