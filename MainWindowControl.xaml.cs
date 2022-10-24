using ClassOutline.Services;
using ClassOutline.TreeNodes;
using EnvDTE;
using EnvDTE80;
using Microsoft.ServiceHub.Resources;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using static ClassOutline.OutlineItem;
using Constants = EnvDTE.Constants;
using Window = EnvDTE.Window;

namespace ClassOutline
{
    /// <summary>
    /// Interaction logic for MainWindowControl.
    /// </summary>
    public partial class MainWindowControl : UserControl
    {
        private DTE _dte = null;
        private DTE2 _dte2 = null;
        /// <summary>
        ///     Reference to environment app
        /// </summary>
        public DTE2 DTE => _dte2;

        private Events2 _dteEvents;
        private WindowEvents _windowEvents;

        private bool isMagicDisplay = true;

        /// <summary>
        /// Initializes a new instance of the <see cref="MainWindowControl"/> class.
        /// </summary>
        public MainWindowControl()
        {
            this.InitializeComponent();

            _dte2 = ServiceProvider.GlobalProvider.GetService(typeof(EnvDTE.DTE)) as DTE2;
            _dteEvents = _dte2.Events as Events2;
            _windowEvents = _dteEvents.WindowEvents;
            _windowEvents.WindowActivated += _windowEvents_WindowActivated;

        }

        private void _windowEvents_WindowActivated(Window GotFocus, Window LostFocus)
        {
            // get the environement
            _dte = ServiceProvider.GlobalProvider.GetService(typeof(DTE)) as DTE;
            RefreshTreeView();
        }

        private ProjectItem getSelectedProjectItem()
        {
            return _dte?.ActiveDocument?.ProjectItem;

        }

        private void RefreshTreeView()
        {
            
            if (DTE==null || DTE.ActiveWindow == null || DTE.ActiveDocument == null || DTE.ActiveDocument?.ProjectItem == null)
            {
                //if no active window, clear the treeview
                ClearTreeView();
            }

            // get the active project item
            var activeProjectItem = getSelectedProjectItem();

            if (activeProjectItem != null)
            {
                ClearTreeView();

                CodeElements codeElements = activeProjectItem.FileCodeModel?.CodeElements;
                var dataSource = new OutlineItem();

                if (codeElements != null)
                    foreach (CodeElement codeElement in codeElements)
                    {
                        createNodes(codeElement, dataSource);
                    }

                treeView.ItemsSource = dataSource.Children;
            }
            
        }

        private void createNodes(CodeElement codeElement, OutlineItem parent)
        {
            switch (codeElement.Kind)
            {
                // in case of namespace, expand each of its member
                case vsCMElement.vsCMElementNamespace:
                    var codeNamespace = codeElement as CodeNamespace;
                    if (codeNamespace != null)
                    {
                        var namespaceMembers = codeNamespace.Members;
                        foreach (CodeElement member in namespaceMembers)
                            createNodes(member, parent);
                    }
                    break;
                // in case of class, keep information we need for the navigation and treeview item
                case vsCMElement.vsCMElementClass: 
                    var codeClass = (CodeClass)codeElement;
                    var treeNode = new ClassTreeNode(codeClass);

                    var child = new OutlineItem();
                    parent.AddChild(child);
                    child.Name = codeClass.Name;
                    child.ProjectItem = codeElement.ProjectItem;
                    child.StartPoint = codeElement.StartPoint;
                    child.EndPoint = codeElement.EndPoint;
                    child.ContextMenuItemEventHandler += OnContextMenuItemCommand;
                    child.OpenProjectItemEventHandler += openProjectItem;
                    child.UpdateViewsEventHandler += UpdateViewItems;
                    
                    // define the icon depending on if it's a business process or a ui controller
                    if (treeNode.BaseClassList.Any(c => c.Contains("UIController")))
                    {
                        child.IsUIController = true;
                        //child.Name += "(UI Controller)";
                    }
                    else
                        child.IsUIController = false;

                    var members = ((CodeClass)codeElement).Members;
                    foreach (CodeElement ce in members)
                        createNodes(ce, child);
                    break;
                default:
                    if (codeElement.Kind == vsCMElement.vsCMElementFunction)
                    {
                        var codeFunction = (CodeFunction2)codeElement;
                        var priority = getMethodPriority(codeFunction);
                        if (priority > 0 || !(bool)MagicDisplay.IsChecked)  // display all methods when it's not in magic
                        {
                            parent.AddMethod(new OutlineItem.Method()
                            {
                                Name = getFunctionName(codeFunction),
                                StartPoint = codeFunction.StartPoint,
                                Priority = priority,
                                Signature = getSignature(codeFunction),
                                Comment = codeFunction.DocComment ?? codeFunction.Comment
                            });
                        }

                    }
                    break;
            }
        }

        private void ClearTreeView()
        {

        }

        /// <summary>
        /// recursively search for the code element where the cursor is.
        /// </summary>
        /// <param name="codeElements"></param>
        /// <param name="cursor"></param>
        private CodeElement GetCodeElementFromCursor(vsCMElement requestedElementKind, CodeElements codeElements, TextPoint cursor)
        {
            if (codeElements != null)
            {
                foreach (CodeElement codeElement in codeElements)
                {
                    // find the code element who is selectionned.
                    if (codeElement.StartPoint.LessThan(cursor) && codeElement.EndPoint.GreaterThan(cursor))
                    {
                        // get the children of this code element
                        CodeElements children = null;
                        if (codeElement is CodeNamespace)
                            children = ((CodeNamespace)codeElement).Members;
                        if (codeElement is CodeType)
                            children = ((CodeType)codeElement).Members;
                        if (codeElement is CodeFunction)
                            children = ((CodeFunction)codeElement).Parameters;

                        if (children != null)
                        {
                            // get one more step deeper in the children genalogy
                            var nextCodeElement = GetCodeElementFromCursor(requestedElementKind, children, cursor);
                            if (nextCodeElement != null)
                                return nextCodeElement; // code to go out of the cycle, return the element we searched
                        }

                        if (codeElement.Kind.Equals(requestedElementKind))
                        {
                            return codeElement; // nextCodeElement is null, means that we have what we want
                        }

                    }
                }
            }
            return null;
        }
        private string getFunctionName(CodeFunction codeFunction)
        {
            var functionName = codeFunction.Name;
            if (codeFunction.FunctionKind == vsCMFunction.vsCMFunctionConstructor) functionName = "Constructor";
            return functionName;
        }
        private string getSignature(CodeFunction2 codeFunction)
        {
            var paramlist = new List<string>();
            foreach (CodeElement p in codeFunction.Parameters)
            {
                var cp = p as CodeParameter;
                if (cp != null)
                {
                    paramlist.Add(string.Format("{0}", cp.Name));
                }
            }


            return getFunctionName(codeFunction) + "(" + string.Join(",", paramlist) + ")";
        }
        private static int getMethodPriority(CodeFunction2 f)
        {
            if (f.Name == "Run") return 100;
            if (f.FunctionKind == vsCMFunction.vsCMFunctionConstructor) return 99;
            if (f.Name.Contains("Initialize")) return 90;
            if (f.OverrideKind == vsCMOverrideKind.vsCMOverrideKindOverride) return 80;
            // dont include
            return 0;

        }
        public string GetImageFullPath(string filename)
        {
            return Path.Combine(
                    //Get the location of your package dll
                    Assembly.GetExecutingAssembly().Location,
                    //reference your 'images' folder
                    "/Resources/",
                    filename
                 );
        }

        private void TreeViewItem_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {

            if (e.Source is TreeViewItem && (e.Source as TreeViewItem).IsSelected)
            {
                // get the outlineItem clicked
                var clickedOutlineItem = (e.Source as TreeViewItem).DataContext as OutlineItem;

                GoToCode(clickedOutlineItem.ProjectItem, clickedOutlineItem.StartPoint);

                e.Handled = true;
            }
        }

        private void OnContextMenuItemCommand(object sender, ContextMenuItemEventArgs args)
        {
            var outlineItem = sender as OutlineItem;
            GoToCode(outlineItem.ProjectItem, args.StartPoint);
        }

        private void UpdateViewItems(object source, UpdateViewsEventArgs args)
        {
            // get the regions
            var outlineItem = source as OutlineItem;
            if (outlineItem == null) return;
            if (outlineItem.ProjectItem.Document == null) return;

            var activeProjectItem = outlineItem.ProjectItem;
            Document document = activeProjectItem.Document;

            var viewParser = new ViewParser();

            var viewTask = viewParser.GetViews(document, outlineItem.StartPoint.Line, outlineItem.StartPoint.LineCharOffset, outlineItem.EndPoint.Line);
            viewTask.Wait();

            var views = viewTask.Result;
            if (views == null) return;

            viewParser.FindView(activeProjectItem, views);

            outlineItem.AddViews(views.Select(
                x =>
                    new OutlineItem.ViewReference()
                    {
                        CodeElement = x.CodeElement,

                        ViewTypeName = x.TypeName
                    }));

        }

        private void openProjectItem(object src, OpenProjectItemEventArgs args)
        {
            var o = src as OutlineItem;
            ProjectItem pi = null;

            var ce = args.CodeElement as CodeElement;
            if (ce != null)
            {
                pi = ce.ProjectItem;
            }
            if (pi == null)
            {
                return;
            }

            var w = pi.Open();
            pi.ExpandView();

            w.Visible = true;
            w.Activate();

            var s = w?.Document?.Selection as TextSelection;
            s?.MoveTo(args.LineNumber, 0);
        }

        /// <summary>
        /// move the cursor in Visual Studio. 
        /// </summary>
        /// <param name="projectItem">page that we want to activate</param>
        /// <param name="startPoint">location in the page where we want the cursor to go</param>
        private void GoToCode(ProjectItem projectItem, TextPoint startPoint)
        {
            // activate the right window
            Window window = projectItem.Open(Constants.vsViewKindCode);
            window.Activate();

            //DTE.ExecuteCommand("Edit.ToggleAllOutlining");

            // select the right line
            var doc = (TextDocument)_dte.ActiveDocument.Object("TextDocument");
            doc.Selection.MoveToPoint(startPoint);

            //DTE.ExecuteCommand("Edit.ToggleOutliningExpansion");
        }

        private void cmdTest_Click(object sender, RoutedEventArgs e)
        {
            RefreshTreeView();
        }


        private void MagicDisplay_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                RefreshTreeView();
            }
            catch (Exception exc)
            {

            }
        }
    }
}