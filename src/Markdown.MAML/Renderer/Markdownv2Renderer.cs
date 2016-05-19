﻿using Markdown.MAML.Model.MAML;
using Markdown.MAML.Parser;
using Markdown.MAML.Resources;
using Markdown.MAML.Transformer;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Markdown.MAML.Renderer
{
    /// <summary>
    /// Renders MamlModel as markdown with schema v 2.0.0
    /// </summary>
    public class MarkdownV2Renderer
    {
        private StringBuilder _stringBuilder = new StringBuilder();

        public int MaxSyntaxWidth { get; private set; }

        public MarkdownV2Renderer() : this(120) { }

        public MarkdownV2Renderer(int maxSyntaxWidth)
        {
            this.MaxSyntaxWidth = maxSyntaxWidth;
        }

        public string MamlModelToString(MamlCommand mamlCommand, bool skipYamlHeader)
        {
            return MamlModelToString(mamlCommand, null, skipYamlHeader);
        }

        public string MamlModelToString(MamlCommand mamlCommand, Hashtable yamlHeader)
        {
            return MamlModelToString(mamlCommand, yamlHeader, false);
        }

        private string MamlModelToString(MamlCommand mamlCommand, Hashtable yamlHeader, bool skipYamlHeader)
        {
            // clear, so we can re-use this instance
            _stringBuilder.Clear();
            if (!skipYamlHeader)
            {
                if (yamlHeader == null)
                {
                    yamlHeader = new Hashtable();
                }

                // put version there
                yamlHeader["schema"] = "2.0.0";
                AddYamlHeader(yamlHeader);
            }

            AddCommand(mamlCommand);

            // at the end, just normalize all ends
            return NormalizeLineEnds(_stringBuilder.ToString());
        }

        private string NormalizeLineEnds(string text)
        {
            return Regex.Replace(text, "\r\n?|\n", "\r\n");
        }

        private void AddYamlHeader(Hashtable yamlHeader)
        {
            _stringBuilder.AppendFormat("---{0}", Environment.NewLine);
            foreach (DictionaryEntry pair in yamlHeader)
            {
                var value = pair.Value == null ? "" : pair.Value.ToString();
                _stringBuilder.AppendFormat("{0}: {1}{2}", pair.Key.ToString(), value, Environment.NewLine);
            }

            _stringBuilder.AppendFormat("---{0}{0}", Environment.NewLine);
        }

        private void AddCommand(MamlCommand command)
        {
            AddHeader(ModelTransformerBase.COMMAND_NAME_HEADING_LEVEL, command.Name, extraNewLine:false);
            AddEntryHeaderWithText(MarkdownStrings.SYNOPSIS, command.Synopsis);
            AddSyntax(command);
            AddEntryHeaderWithText(MarkdownStrings.DESCRIPTION, command.Description);
            AddExamples(command);
            AddParameters(command);
            AddInputs(command);
            AddOutputs(command);
            AddEntryHeaderWithText(MarkdownStrings.NOTES, command.Notes);
            AddLinks(command);
        }

        private void AddLinks(MamlCommand command)
        {
            AddHeader(ModelTransformerBase.COMMAND_ENTRIES_HEADING_LEVEL, MarkdownStrings.RELATED_LINKS);
            foreach (var link in command.Links)
            {
                var name = link.LinkName;
                if (string.IsNullOrEmpty(name))
                {
                    // we need a valid name to produce a valid markdown
                    name = link.LinkUri;
                }

                _stringBuilder.AppendFormat("[{0}]({1}){2}{2}", name, link.LinkUri, Environment.NewLine);
            }
        }

        private void AddInputOutput(MamlInputOutput io)
        {
            if (string.IsNullOrEmpty(io.TypeName) && string.IsNullOrEmpty(io.Description))
            {
                // in this case ignore
                return;
            }

            var extraNewLine = string.IsNullOrEmpty(io.Description);
            AddHeader(ModelTransformerBase.INPUT_OUTPUT_TYPENAME_HEADING_LEVEL, io.TypeName, extraNewLine);
            AddParagraphs(io.Description);
        }

        private void AddOutputs(MamlCommand command)
        {
            AddHeader(ModelTransformerBase.COMMAND_ENTRIES_HEADING_LEVEL, MarkdownStrings.OUTPUTS);
            foreach (var io in command.Outputs)
            {
                AddInputOutput(io);
            }
        }

        private void AddInputs(MamlCommand command)
        {
            AddHeader(ModelTransformerBase.COMMAND_ENTRIES_HEADING_LEVEL, MarkdownStrings.INPUTS);
            foreach (var io in command.Inputs)
            {
                AddInputOutput(io);
            }
        }

        private void AddParameters(MamlCommand command)
        {
            AddHeader(ModelTransformerBase.COMMAND_ENTRIES_HEADING_LEVEL, MarkdownStrings.PARAMETERS);
            foreach (var param in command.Parameters)
            {
                AddParameter(param, command);
            }

            if (command.IsWorkflow)
            {
                AddWorkflowParameters();
            }

            if (command.SupportCommonParameters)
            {
                AddCommonParameters();
            }
        }

        private void AddCommonParameters()
        {
            AddHeader(ModelTransformerBase.PARAMETERSET_NAME_HEADING_LEVEL, MarkdownStrings.CommonParametersToken, extraNewLine: false);
            AddParagraphs(MarkdownStrings.CommonParametersText);
        }

        private void AddWorkflowParameters()
        {
            AddHeader(ModelTransformerBase.PARAMETERSET_NAME_HEADING_LEVEL, MarkdownStrings.WorkflowParametersToken, extraNewLine: false);
            AddParagraphs(MarkdownStrings.WorkflowParametersText);
        }

        private Dictionary<string, MamlParameter> GetParamSetDictionary(string parameterName, List<MamlSyntax> syntaxes)
        {
            var result = new Dictionary<string, MamlParameter>();
            foreach (var syntax in syntaxes)
            {
                foreach (var param in syntax.Parameters)
                {
                    if (StringComparer.OrdinalIgnoreCase.Equals(parameterName, param.Name))
                    {
                        result[syntax.ParameterSetName] = param;
                        // there could be only one parameter in the param set with the same name
                        break;
                    }
                }
            }
            return result;
        }

        private List<Tuple<List<string>, MamlParameter>> SimplifyParamSets(Dictionary<string, MamlParameter> parameterMap)
        {
            var res = new List<Tuple<List<string>, MamlParameter>>();
            // using a O(n^2) algorithm, because it's simpler and n is very small.
            foreach (var pair in parameterMap)
            {
                var seekValue = pair.Value;
                var paramSetName = pair.Key;
                bool found = false;
                foreach (var tuple in res)
                {
                    if (tuple.Item2.IsMetadataEqual(seekValue))
                    {
                        tuple.Item1.Add(paramSetName);
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    // create a new entry
                    var paramSets = new List<string>();
                    paramSets.Add(paramSetName);
                    res.Add(new Tuple<List<string>, MamlParameter>(paramSets, seekValue));
                }
            }

            return res;
        }

        private string JoinWithComma(IEnumerable<string> args)
        {
            if (args == null)
            {
                return "";
            }

            return string.Join(", ", args);
        }

        private void AddParameter(MamlParameter parameter, MamlCommand command)
        {
            AddHeader(ModelTransformerBase.PARAMETERSET_NAME_HEADING_LEVEL, '-' + parameter.Name, extraNewLine: false);
            AddParagraphs(parameter.Description);
            
            var sets = SimplifyParamSets(GetParamSetDictionary(parameter.Name, command.Syntax));
            foreach (var set in sets)
            {
                _stringBuilder.AppendFormat("```yaml{0}", Environment.NewLine);

                _stringBuilder.AppendFormat("{0}: {1}{2}", MarkdownStrings.Type, parameter.Type, Environment.NewLine);
                if (command.Syntax.Count > 1)
                {
                    // ignore, if there is just one parameter set
                    _stringBuilder.AppendFormat("{0}: {1}{2}", MarkdownStrings.Parameter_Sets, JoinWithComma(set.Item1), Environment.NewLine);
                }

                _stringBuilder.AppendFormat("{0}: {1}{2}", MarkdownStrings.Aliases, JoinWithComma(parameter.Aliases), Environment.NewLine);
                if (parameter.ParameterValueGroup.Count > 0)
                {
                    _stringBuilder.AppendFormat("{0}: {1}{2}", MarkdownStrings.Accepted_values, JoinWithComma(parameter.ParameterValueGroup), Environment.NewLine);
                }
                _stringBuilder.AppendLine();

                _stringBuilder.AppendFormat("{0}: {1}{2}", MarkdownStrings.Required, set.Item2.Required, Environment.NewLine);
                _stringBuilder.AppendFormat("{0}: {1}{2}", MarkdownStrings.Position, set.Item2.IsNamed() ? "Named" : set.Item2.Position, Environment.NewLine);
                _stringBuilder.AppendFormat("{0}: {1}{2}", MarkdownStrings.Default_value, parameter.DefaultValue, Environment.NewLine);
                _stringBuilder.AppendFormat("{0}: {1}{2}", MarkdownStrings.Accept_pipeline_input, parameter.PipelineInput, Environment.NewLine);
                _stringBuilder.AppendFormat("{0}: {1}{2}", MarkdownStrings.Accept_wildcard_characters, parameter.Globbing, Environment.NewLine);

                _stringBuilder.AppendFormat("```{0}{0}", Environment.NewLine);
            }
        }

        private void AddExamples(MamlCommand command)
        {
            AddHeader(ModelTransformerBase.COMMAND_ENTRIES_HEADING_LEVEL, MarkdownStrings.EXAMPLES);
            foreach (var example in command.Examples)
            {
                AddHeader(ModelTransformerBase.EXAMPLE_HEADING_LEVEL, example.Title, extraNewLine: false);
                if (!string.IsNullOrEmpty(example.Introduction))
                {
                    AddParagraphs(example.Introduction);
                }

                if (example.Code != null)
                {
                    AddCodeSnippet(example.Code);
                }

                if (!string.IsNullOrEmpty(example.Remarks))
                {
                    AddParagraphs(example.Remarks);
                }
            }
        }

        private string GetSyntaxString(MamlCommand command, MamlSyntax syntax)
        {
            // TODO: we may want to add ParameterValueGroup info here,
            // but it's fine for now

            var sb = new StringBuilder();
            sb.Append(command.Name);

            var paramStrings = new List<string>();
                        
            // first we create list of param string we want to add
            foreach (var param in syntax.Parameters)
            {
                string paramStr;
                if (param.IsSwitchParameter())
                {
                    paramStr = string.Format("[-{0}]", param.Name);
                }
                else
                {
                    paramStr = string.Format("-{0}", param.Name);
                    if (!param.IsNamed())
                    {
                        // for positional parameters, we can avoid specifying the name
                        paramStr = string.Format("[{0}]", paramStr);
                    }

                    paramStr = string.Format("{0} <{1}>", paramStr, param.Type);
                    if (!param.Required)
                    {
                        paramStr = string.Format("[{0}]", paramStr);
                    }
                }
                paramStrings.Add(paramStr);   
            }

            if (command.IsWorkflow)
            {
                paramStrings.Add("[<" + MarkdownStrings.WorkflowParametersToken + ">]");
            }

            if (command.SupportCommonParameters)
            {
                paramStrings.Add("[<" + MarkdownStrings.CommonParametersToken + ">]");
            }

            // then we format them properly with repsect to max width for window.
            int widthBeforeLastBreak = 0;
            foreach (string paramStr in paramStrings) { 

                if (sb.Length - widthBeforeLastBreak + paramStr.Length > this.MaxSyntaxWidth)
                {
                    sb.AppendLine();
                    widthBeforeLastBreak = sb.Length;
                }

                sb.AppendFormat(" {0}", paramStr);
            }

            return sb.ToString();
        }

        private void AddSyntax(MamlCommand command)
        {
            AddHeader(ModelTransformerBase.COMMAND_ENTRIES_HEADING_LEVEL, MarkdownStrings.SYNTAX);
            foreach (var syntax in command.Syntax)
            {
                if (command.Syntax.Count > 1)
                {
                    AddHeader(ModelTransformerBase.PARAMETERSET_NAME_HEADING_LEVEL, syntax.ParameterSetName, extraNewLine: false);
                }

                AddCodeSnippet(GetSyntaxString(command, syntax));
            }
        }

        private void AddEntryHeaderWithText(string header, string text)
        {
            // we want indentation, if there is no text inside
            var extraNewLine = string.IsNullOrWhiteSpace(text);
            AddHeader(ModelTransformerBase.COMMAND_ENTRIES_HEADING_LEVEL, header, extraNewLine);
            AddParagraphs(text);
        }

        private void AddCodeSnippet(string code, string lang = "")
        {
            _stringBuilder.AppendFormat("```{1}{2}{0}{2}```{2}{2}", code, lang, Environment.NewLine);
        }

        private void AddHeader(int level, string header, bool extraNewLine = true)
        {
            for (int i = 0; i < level; i++)
            {
                _stringBuilder.Append('#');
            }
            _stringBuilder.Append(' ');
            _stringBuilder.AppendFormat("{0}{1}", header, Environment.NewLine);
            if (extraNewLine)
            {
                _stringBuilder.Append(Environment.NewLine);
            }
        }

        private string GetAutoWrappingForNonListLine(string line)
        {
            return Regex.Replace(line, @"([\.\!\?]) ( )*([^\r\n])", "$1$2\r\n$3");
        }

        private string GetAutoWrappingForMarkdown(string[] lines)
        {
            // this is an implementation of https://github.com/PowerShell/platyPS/issues/93
            
            // algorithm: identify chunks that represent lists
            // Every entry in a list should be preserved as is and 1 EOL between them
            // Every entry not in a list should be split with GetAutoWrappingForNonListLine
            // delimiters between lists and non-lists are 2 EOLs.

            var newLines = new List<string>();

            for (int i = 0; i < lines.Length; i++)
            {
                if (MarkdownParser.HasListPrefix(lines[i]))
                {
                    if (i > 0 && !MarkdownParser.HasListPrefix(lines[i - 1]))
                    {
                        // we are in a list and it just started
                        newLines.Add(Environment.NewLine + lines[i]);
                    }
                    else
                    {
                        newLines.Add(lines[i]);
                    }
                }
                else
                {
                    if (i > 0)
                    {
                        // we are just finished a list
                        newLines.Add(Environment.NewLine + GetAutoWrappingForNonListLine(lines[i]));
                    }
                    else
                    {
                        newLines.Add(GetAutoWrappingForNonListLine(lines[i]));
                    }
                }
            }

            return string.Join(Environment.NewLine, newLines);
        }

        private void AddParagraphs(string body)
        {
            if (!string.IsNullOrWhiteSpace(body))
            {
                body = RenderHyperLinks(body);
                string[] paragraphs = body.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

                var text = GetAutoWrappingForMarkdown(paragraphs.Select(para => GetEscapedMarkdownText(para.Trim())).ToArray());
                _stringBuilder.AppendFormat("{0}{1}{1}", text, Environment.NewLine);
            }
        }

        public static string RenderHyperLinks(string text)
        {
            string linkMatchString = string.Format("{0}{1}{2}{3}{4}",
                MamlLink.HYPERLINK_START_MARKER,
                "(.*)",
                MamlLink.HYPERLINK_MIDDLE_MARKER,
                "(.*)",
                MamlLink.HYPERLINK_END_MARKER);

            return Regex.Replace(text, linkMatchString, new MatchEvaluator(HyperLinkEvaluator));
        }

        private static string HyperLinkEvaluator(Match match)
        {
            var text = match.Groups[1].Value;
            var uri = match.Groups[2].Value;
            return string.Format("[{0}]({1})", text, uri);
        }

        private static string BackSlashMatchEvaluater(Match match)
        {
            // '\<' -> '\\<'
            // '\\<' -> '\\<' - noop
            // '\\\<' -> '\\\\<'

            var g1 = match.Groups[1].Value;
            var g2 = match.Groups[2].Value[0];

            if (g1.Length % 2 == 0 && "<>()[]`".Contains(g2))
            {
                return @"\" + match.Value;
            }

            return match.Value;
        }

        // public just to make testing easier
        public static string GetEscapedMarkdownText(string text)
        {
            // this is kind of a crazy replacement to handle escaping properly.
            // we need to do the reverse operation in our markdown parser.

            // PS code: (((($text - replace '\\\\','\\\\') -replace '([<>])','\$1') -replace '\\([\[\]\(\)])', '\\$1')

            // examples: 
            // '\<' -> '\\\<'
            // '\' -> '\'
            // '\\' -> '\\\\'
            // '<' -> '\<'

            text = text
                .Replace(@"\\", @"\\\\");

            text = Regex.Replace(text, @"(\\*)\\(.)", new MatchEvaluator(BackSlashMatchEvaluater));

            return text
                .Replace(@"<", @"\<")
                .Replace(@">", @"\>")

                .Replace(@"[", @"\[")
                .Replace(@"]", @"\]")
                .Replace(@"(", @"\(")
                .Replace(@")", @"\)")
                .Replace(@"`", @"\`")

                ;
        }
    }
}
