﻿// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using System.Text;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.DotNet.Interactive.App.ParserServer;
using Newtonsoft.Json;
using Xunit;

namespace Microsoft.DotNet.Interactive.Documents.Tests;

public partial class NotebookParserServerTests
{
    public class Deserialization
    {
        [Theory]
        [InlineData(DocumentSerializationType.Dib, "#!csharp\nvar x = 1;")]
        [InlineData(DocumentSerializationType.Ipynb, """{"cells":[{"cell_type":"code","source":["var x = 1;"]}]}""")]
        public void Notebook_parser_server_can_parse_file_based_on_document_type(
            DocumentSerializationType serializationType,
            string contents)
        {
            var request = new NotebookParseRequest(
                "the-id",
                serializationType,
                defaultLanguage: "csharp",
                rawData: Encoding.UTF8.GetBytes(contents));

            var response = NotebookParserServer.HandleRequest(request);

            response
                .Should()
                .BeOfType<NotebookParseResponse>()
                .Which
                .Document
                .Elements
                .Should()
                .ContainSingle()
                .Which
                .Contents
                .Should()
                .Be("var x = 1;");
        }

        [Fact]
        public void Notebook_parser_server_can_parse_a_dib_file_with_well_known_kernel_metadata()
        {
            var dibContents = """

                              #!meta
                              {
                                "kernelInfo": {
                                  "defaultKernelName": "csharp",
                                  "items": [
                                    {
                                      "name": "csharp",
                                      "languageName": "csharp"
                                    },
                                    {
                                      "name": "fsharp",
                                      "languageName": "fsharp"
                                    }
                                  ]
                                }
                              }

                              #!csharp

                              var x = 1; // this is C#

                              #!fsharp

                              let x = 1 (* this is F# *)

                              """;
            var request = new NotebookParseRequest(
                "the-id",
                DocumentSerializationType.Dib,
                defaultLanguage: "csharp",
                rawData: Encoding.UTF8.GetBytes(dibContents));

            var response = NotebookParserServer.HandleRequest(request);

            response
                .Should()
                .BeOfType<NotebookParseResponse>()
                .Which
                .Document
                .Elements
                .Select(e => e.KernelName)
                .Should()
                .Equal(["csharp", "fsharp"]);
        }

        [Fact]
        public void Notebook_parser_server_can_parse_a_dib_file_with_not_well_known_kernel_metadata()
        {
            var dibContents = """

                              #!meta
                              {
                                "kernelInfo": {
                                  "defaultKernelName": "snake-language",
                                  "items": [
                                    {
                                      "name": "snake-language",
                                      "languageName": "python"
                                    }
                                  ]
                                }
                              }

                              #!snake-language

                              x = 1 # this is Python

                              """.Trim();
            var request = new NotebookParseRequest(
                "the-id",
                DocumentSerializationType.Dib,
                defaultLanguage: "csharp",
                rawData: Encoding.UTF8.GetBytes(dibContents));

            var response = NotebookParserServer.HandleRequest(request);

            using var _ = new AssertionScope();

            var document = response
                           .Should()
                           .BeOfType<NotebookParseResponse>()
                           .Which
                           .Document;

            document
                .Elements
                .Single()
                .KernelName
                .Should()
                .Be("snake-language");

            var kernelInfo = document.GetKernelInfo()
                                     .Should()
                                     .BeOfType<KernelInfoCollection>()
                                     .Which;

            kernelInfo
                .DefaultKernelName
                .Should()
                .Be("snake-language");

            kernelInfo
                .Should().Contain(i => i.LanguageName == "python");
        }

        [Fact]
        public void Notebook_parser_server_can_parse_a_dib_file_with_merged_kernel_metadata()
        {
            var dibContents = """

                              #!meta
                              {
                                "kernelInfo": {
                                  "defaultKernelName": "csharp",
                                  "items": [
                                    {
                                      "name": "csharp",
                                      "languageName": "csharp"
                                    }
                                  ]
                                }
                              }

                              #!csharp

                              var x = 1; // this is C#

                              #!fsharp

                              let x = 1 (* this is F# *)

                              """;
            var request = new NotebookParseRequest(
                "the-id",
                DocumentSerializationType.Dib,
                defaultLanguage: "csharp",
                rawData: Encoding.UTF8.GetBytes(dibContents));

            var response = NotebookParserServer.HandleRequest(request);

            response
                .Should()
                .BeOfType<NotebookParseResponse>()
                .Which
                .Document
                .Elements
                .Select(e => e.KernelName)
                .Should()
                .Equal(["csharp", "fsharp"]);
        }

        [Fact]
        public void Notebook_parser_server_can_parse_a_ipynb_file_with_well_known_kernel_metadata()
        {
            var ipynb = new
            {
                cells = new object[]
                {
                    new
                    {
                        cell_type = "code",
                        execution_count = 0,
                        source = new[] { "let x  = 1" },
                        metadata = new
                        {
                            polyglot_notebook = new
                            {
                                kernelName = "fsharp"
                            }
                        }
                    },
                    new
                    {
                        cell_type = "code",
                        execution_count = 0,
                        source = new[] { "var x = 123;" },
                        metadata = new
                        {
                            polyglot_notebook = new
                            {
                                kernelName = "csharp"
                            }
                        }
                    }
                },
                metadata = new
                {
                    kernelspec = new
                    {
                        display_name = ".NET (C#)",
                        language = "C#",
                        name = ".net-csharp"
                    },
                    language_info = new
                    {
                        file_extension = ".cs",
                        mimetype = "text/x-csharp",
                        name = "C#",
                        pygments_lexer = "csharp",
                        version = "11.0"
                    },
                    polyglot_notebook = new
                    {
                        defaultKernelName = "csharp",
                        items = new object[]
                        {
                            new
                            {
                                name = "csharp",
                                languageName = "csharp"
                            },
                            new
                            {
                                name = "fsharp",
                                languageName = "fsharp"
                            }
                        }
                    }
                },
                nbformat = 4,
                nbformat_minor = 4
            };

            var ipynbContents = JsonConvert.SerializeObject(ipynb);

            var request = new NotebookParseRequest(
                "the-id",
                DocumentSerializationType.Ipynb,
                defaultLanguage: "csharp",
                rawData: Encoding.UTF8.GetBytes(ipynbContents));

            var response = NotebookParserServer.HandleRequest(request);

            response
                .Should()
                .BeOfType<NotebookParseResponse>()
                .Which
                .Document
                .Elements
                .Select(e => e.KernelName)
                .Should()
                .Equal(["fsharp", "csharp"]);
        }

        [Fact]
        public void Notebook_parser_server_can_parse_a_ipynb_file_with_not_well_known_kernel_metadata()
        {
            var ipynb = new
            {
                cells = new object[]
                {
                    new
                    {
                        cell_type = "code",
                        execution_count = 0,
                        source = new[] { "x  = 1" },
                        metadata = new
                        {
                            polyglot_notebook = new
                            {
                                kernelName = "snake-language"
                            }
                        }
                    }
                },
                metadata = new
                {
                    kernelspec = new
                    {
                        display_name = ".NET (C#)",
                        language = "C#",
                        name = ".net-csharp"
                    },
                    language_info = new
                    {
                        file_extension = ".cs",
                        mimetype = "text/x-csharp",
                        name = "C#",
                        pygments_lexer = "csharp",
                        version = "11.0"
                    },
                    polyglot_notebook = new
                    {
                        defaultKernelName = "csharp",
                        items = new object[]
                        {
                            new
                            {
                                name = "csharp",
                                languageName = "csharp"
                            },
                            new
                            {
                                name = "snake-language",
                                languageName = "python"
                            }
                        }
                    }
                },
                nbformat = 4,
                nbformat_minor = 4
            };

            var ipynbContents = JsonConvert.SerializeObject(ipynb);

            var request = new NotebookParseRequest(
                "the-id",
                DocumentSerializationType.Ipynb,
                defaultLanguage: "csharp",
                rawData: Encoding.UTF8.GetBytes(ipynbContents));

            var response = NotebookParserServer.HandleRequest(request);

            response
                .Should()
                .BeOfType<NotebookParseResponse>()
                .Which
                .Document
                .Elements
                .Single()
                .KernelName
                .Should()
                .Be("snake-language");
        }

        [Fact]
        public void Notebook_parser_server_throws_on_unsupported_document_type()
        {
            var request = new NotebookParseRequest(
                "the-id",
                serializationType: (DocumentSerializationType)42,
                defaultLanguage: "csharp",
                rawData: []);
            var handle = () => NotebookParserServer.HandleRequest(request);

            handle.Invoking(h => h())
                  .Should()
                  .Throw<NotSupportedException>()
                  .Which
                  .Message
                  .Should()
                  .Contain($"Unable to parse an interactive document with type '{(int)request.SerializationType}'");
        }
    }
}