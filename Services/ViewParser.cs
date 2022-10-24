using EnvDTE;
using Microsoft.VisualStudio.Text.Outlining;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static ClassOutline.Services.ViewParser;

namespace ClassOutline.Services
{
    public class ViewParser
    {
        private string _documentText;

        public async Task<IEnumerable<ViewAssignment>> GetViews(Document document, int startline, int startLineOfCodeOffset, int endline)
        {
            _documentText = getDocumentText(document, startline, startLineOfCodeOffset, endline);

            if (_documentText == null) return null;
            var assignments = GetViewAssignments(_documentText, startline);
            var filteredAssignments = removeAssignmentsNotInClass(assignments, document, startline, startLineOfCodeOffset);

            return filteredAssignments;
        }
        /// <summary>
        /// Find views in a project
        /// </summary>
        /// <param name="activeProjectItem"></param>
        /// <param name="views"></param>
        public void FindView(ProjectItem activeProjectItem, IEnumerable<ViewAssignment> views)
        {
            if (views == null) return;
            if (activeProjectItem == null) return;

            foreach (var view in views)
            {
                if (view != null)
                {
                    Debug.WriteLine(view.TypeName);

                    Project parent = activeProjectItem.Collection.Parent as Project;
                    ProjectItems projectItems = null;

                    if (parent == null)
                    {
                        var pi = activeProjectItem.Collection.Parent as ProjectItem;
                        if (pi != null)
                        {
                            projectItems = pi.ProjectItems;
                        }
                        else
                        {
                            projectItems = activeProjectItem.ContainingProject.ProjectItems;
                        }

                    }
                    else
                    {
                        projectItems = parent.ProjectItems;
                    }

                    view.CodeElement = () => FindType(projectItems, view.TypeName);
                }
            }
        }
        private CodeElement FindType(ProjectItems projectItems, string typename)
        {
            var tokens = typename.Split('.');
            var path = new Queue<string>(tokens.ToList());


            while (path.Count > 0)
            {
                var itemName = path.Dequeue();
                var found = false;

                if (projectItems == null) break;

                foreach (ProjectItem projectItem in projectItems)
                {
                    if (projectItem.Name.Equals(itemName, StringComparison.CurrentCultureIgnoreCase))
                    {
                        found = true;

                        if (projectItem.ProjectItems != null && projectItem.ProjectItems.Count > 0)
                        {
                            // search the children of this projectitem
                            var foundHere = FindType(projectItem.ProjectItems, string.Join(".", path.ToArray()));

                            if (foundHere != null)
                            {
                                return foundHere;
                            }

                            break;
                        }
                    }
                    else
                    {
                        var theType = FindType(projectItem, typename);

                        if (theType != null)
                        {
                            return theType;
                        }
                    }

                }
                if (!found)
                {
                    break;
                }
            }
            return null;
        }
        private CodeElement FindType(ProjectItem parent, string typename)
        {
            if (parent.FileCodeModel != null)
            {
                return FindType(parent.FileCodeModel.CodeElements, typename);

            }
            return null;
        }

        private CodeElement FindType(CodeElements codeElements, string typename)
        {
            foreach (CodeElement codeElement in codeElements)
            {

                if (codeElement.Kind == vsCMElement.vsCMElementNamespace)
                {
                    var ret = FindType(codeElement.Children, typename);
                    if (ret != null)
                    {
                        return ret;
                    }
                }
                if (codeElement.Kind != vsCMElement.vsCMElementClass) continue;
                Debug.WriteLine(codeElement.FullName);
                if (codeElement.Name == typename || codeElement.FullName == typename || codeElement.FullName.EndsWith(typename))
                {
                    return codeElement;
                }
                if (codeElement.Children != null)
                {
                    var ret = FindType(codeElement.Children, typename);
                    if (ret != null)
                    {
                        return ret;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// get the Text of the outlined item
        /// </summary>
        /// <param name="outlineItem">outline item</param>
        /// <returns> text link to the outlineitem</returns>
        public string getDocumentText(Document document, int startline, int startLineOfCodeOffset, int endline)
        {
            if (document == null) return null;
            try
            {
                var textDocument = (TextDocument)document.Object("TextDocument");
                EditPoint editPoint = null;
                if (startline > 0)
                {
                    editPoint = textDocument.CreateEditPoint();
                    return editPoint.GetLines(startline, endline);
                }
                editPoint = textDocument.StartPoint.CreateEditPoint();
                return editPoint.GetText(textDocument.EndPoint);
            }
            catch
            {
                return null;
            }
        }
        public class ViewAssignment
        {
            public string TypeName { get; set; }
            public int LineNumber { get; set; }
            public Func<CodeElement> CodeElement { get; set; }
            public int LineOffset { get; set; }
        }

        public IEnumerable<ViewAssignment> GetViewAssignments(string source, int startline)
        {
            var result = new List<ViewAssignment>();

            var r = new Regex(@"View\s?=\s?\(\)\s?=>\s?new\s?");
            foreach (Match m in r.Matches(source))
            {
                var start = m.Index;
                var l = m.Length;
                var nextline = source.IndexOf('\r', start);
                start += l;
                var found = source.Substring(start, nextline - start);
                var lineNumber = source.Substring(0, start - 1).Count(c => c == '\r');

                var lineEnd = found.IndexOfAny(new[] { '(', ';' });

                var typename = found.Substring(0, lineEnd);

                result.Add(new ViewAssignment() { TypeName = typename, LineNumber = lineNumber + startline, LineOffset = start - start });


            }
            return result;

        }

        private IEnumerable<ViewAssignment> removeAssignmentsNotInClass(IEnumerable<ViewAssignment> assignments, Document document, int startline, int startLineOfCodeOffset)
        {
            var parentCE = getCodeElementFromLine(document, startline, startLineOfCodeOffset);

            var result = new List<ViewAssignment>();
            foreach (var assignement in assignments)
            {
                CodeElement viewCE = null;
                if (assignement.CodeElement != null) viewCE = assignement.CodeElement();
                if (viewCE == null)
                {
                    viewCE = getCodeElementFromLine(document, startline, startLineOfCodeOffset);
                }
                if (viewCE != null)
                {
                    if (viewCE.FullName == parentCE.FullName)
                    {
                        result.Add(assignement);
                    }
                }
            }
            return result;
        }
        private CodeElement getCodeElementFromLine(Document document, int startline, int startLineOfCodeOffset)
        {
            var textDocument = (TextDocument)document.Object("TextDocument");
            EditPoint editPoint = null;
            if (startline >= 0)
            {
                try
                {
                    editPoint = textDocument.CreateEditPoint();
                    editPoint.MoveToLineAndOffset(startline, Math.Max(1, startLineOfCodeOffset));

                    return editPoint.CodeElement[vsCMElement.vsCMElementClass];
                }
                catch (COMException e)
                {
                    
                }

            }
            return null;
        }
    }
}
