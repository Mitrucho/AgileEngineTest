using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HtmlAgilityPack;

namespace HtmlAnalyzer
{
    class Program
    {
        /// <summary>
        /// The Id of the element to search in Original HTML File
        /// </summary>
        private static string TargetElementId = "make-everything-ok-button";

        /// <summary>
        /// Original HTML File, where we are going to search an element
        /// </summary>
        private static string _OriginalFilePath;

        /// <summary>
        /// Any other HTML File to find the element
        /// </summary>
        private static string _DiffCaseFilePath;

        /// <summary>
        /// Simple Sturcture to understand how the target node is similar to the original in points
        /// </summary>
        private struct NodeCounted
        {
            /// <summary>
            /// Diff-Case fetched node
            /// </summary>
            public HtmlNode Node { get; set; }

            /// <summary>
            /// The sum of the matches found between the attributes
            /// </summary>
            public int MatchesCount { get; set; }
        }

        /// <summary>
        /// Helper to check if file exists in File System
        /// </summary>
        /// <param name="HtmlFilePath">Path to the file in the local system</param>
        /// <param name="Hint">Just some label for error information</param>
        /// <returns></returns>
        static string CheckFileExists(string HtmlFilePath, string Hint)
        {
            if (string.IsNullOrEmpty(HtmlFilePath))
                throw new ArgumentNullException($"Parameter is missing : '{Hint} path' !");

            if (!File.Exists(HtmlFilePath))
                throw new FileNotFoundException($"{Hint} is not found in the destination path : '{HtmlFilePath}' ");

            return HtmlFilePath;
        }

        /// <summary>
        /// Check initial arguments and assign values to programm variables
        /// </summary>
        /// <param name="args">Arguments for Console Application</param>
        static void SetupParserArguments(ref string[] args)
        {
            if (args.Length < 2)
                DisplayUsage();

            _OriginalFilePath = CheckFileExists(args[0], "Original File");
            _DiffCaseFilePath = CheckFileExists(args[1], "Sample File");

            if (args.Length == 3)
                TargetElementId = args[2];
        }

        /// <summary>
        /// Method-Hint for User to explain the usage of the programm
        /// </summary>
        static void DisplayUsage()
        {
            Console.WriteLine("HTMLAnalyzer.exe {InputOriginFilePath} {InputOtherSampleFilePath}");
        }


        /// <summary>
        /// Helper Method to simply load Html Document into memory object
        /// </summary>
        /// <param name="Path">Physical path in the file system to the file</param>
        /// <param name="NewHtmlDocument">Output variable result</param>
        private static void LoadDocument(string Path, out HtmlDocument NewHtmlDocument)
        {
            NewHtmlDocument = new HtmlDocument();

            try
            {
                NewHtmlDocument.Load(Path);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occured when loading the document `{Path}` : {ex.Message}");
                Environment.Exit(1);
            }
        }

        /// <summary>
        /// Get the node, whose attributes keys and values has the highest value
        /// </summary>
        /// <param name="FetchedNodes">Collection of the simillar nodes in diff-case file</param>
        /// <param name="OriginalNode">Original element node</param>
        /// <returns>Node from the HTML File that matches the original the most</returns>
        private static HtmlNode GetMostSimilarNode(HtmlNodeCollection FetchedNodes, HtmlNode OriginalNode)
        {
            List<NodeCounted> Counts = new List<NodeCounted>();

            //Looping though all the result
            foreach (var Node in FetchedNodes)
            {
                int MatchesCount = 0;

                foreach (var attr in OriginalNode.Attributes)
                {
                    if (Node.Attributes.Contains(attr.Name))
                    {
                        // In case attribute's value equals -> increase the match counter
                        if (Node.Attributes[attr.Name].Value == OriginalNode.Attributes[attr.Name].Value)
                            MatchesCount++;
                    }
                }

                //Also +1 point to Gryffindor in case inner text is the same
                if (OriginalNode.InnerHtml == Node.InnerHtml)
                    MatchesCount++;

                //Adding result count to middleware repository
                Counts.Add(new NodeCounted() { Node = Node, MatchesCount = MatchesCount });
            }

            //Finally return the winner
            return Counts.OrderByDescending(r => r.MatchesCount).Select(x => x.Node).FirstOrDefault();
        }


        /// <summary>
        /// Main procedure of the programm
        /// 1. Loading Documents
        /// 2. Find an Element in Original File
        /// 3. Try to find the same element by Id in diff-case File
        /// 4. Handle matches
        /// 5. Output the XPath
        /// </summary>
        static void DoWork()
        {

            //Loading Original Document
            LoadDocument(_OriginalFilePath, out HtmlDocument originalDocument);
            
            //Loading Diff-Case Document
            LoadDocument(_DiffCaseFilePath, out HtmlDocument sampleDocument);

            try
            {
                Console.WriteLine($"Searching for element with ID `{TargetElementId}`");

                HtmlNode OriginalButtonNode = originalDocument.GetElementbyId(TargetElementId);
                if (OriginalButtonNode == null)
                    throw new Exception($"Element with ID `{TargetElementId}` was not found :-(");

                HtmlNode DiffCaseButtonNode = sampleDocument.GetElementbyId(TargetElementId);


                string ResultXPathQuery = string.Empty;

                if (DiffCaseButtonNode == null)
                {
                    //Getting the expression for XPath for all the key/value pairs
                    string AttributesExpression = string.Join(" or ", OriginalButtonNode.Attributes.Select(r => $"@{r.Name}='{r.Value}'"));

                    //Concatinating all the pieces into full expression
                    string SearchXPathQuery = $"//a[{AttributesExpression} or text()='{OriginalButtonNode.InnerText}']";

                    var nodes = sampleDocument.DocumentNode.SelectNodes(SearchXPathQuery);
                    if (nodes.Count > 1)
                    {
                        ResultXPathQuery = GetMostSimilarNode(nodes, OriginalButtonNode)?.XPath;

                    }
                    else if (nodes.Count == 1)
                    {
                        ResultXPathQuery = nodes[0]?.XPath;
                    }
                }
                else
                {
                    //If we are lucky enough
                    ResultXPathQuery = DiffCaseButtonNode.XPath;
                }

                if (string.IsNullOrEmpty(ResultXPathQuery))
                { 
                    Console.WriteLine("Search element was not found in the diff-case html page :-( . Something went wrong...");
                    Environment.Exit(1);
                }
                else
                {
                    ProcessResult(ref ResultXPathQuery);
                    Environment.Exit(0);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error trying to find the element by ID: {0}", ex.Message);
                Environment.Exit(1);
            }
        }

        private static void ProcessResult(ref string XPath)
        {
            if (!Directory.Exists("Output"))
                Directory.CreateDirectory("Output");

            string newFileName = $"Output\\{Path.GetFileNameWithoutExtension(_DiffCaseFilePath)}.txt";
            File.WriteAllText(newFileName, XPath);

            Console.WriteLine(XPath);
        }
        

        /// <summary>
        /// Entry point of the Programm
        /// </summary>
        /// <param name="args">Arguments for Console Application</param>
        static void Main(string[] args)
        {
            SetupParserArguments(ref args);

            DoWork();
        }
    }
}