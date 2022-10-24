using ClassOutline.Classes;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Package;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Constants = EnvDTE.Constants;

namespace ClassOutline
{
    public class OutlineItem : INotifyPropertyChanged
    {
        private string _name;
        public string Name
        {
            get { return _name; }
            set
            {
                if (value == _name) return;
                _name = value;
                OnPropertyChanged();
            }
        }
        public bool IsUIController { get; set; }
        public ProjectItem ProjectItem { get; set; }
        public TextPoint StartPoint { get; set; }
        public TextPoint EndPoint { get; set; }
        public Uri ImageUri { get; set; }

        public EventHandler<OpenProjectItemEventArgs> OpenProjectItemEventHandler;
        private List<ContextMenuItem> _menuItems;
        public EventHandler<ContextMenuItemEventArgs> ContextMenuItemEventHandler;
        private IList<ContextMenuItem> _viewsMenu;
        private IEnumerable<ViewReference> _views;
        public EventHandler<UpdateViewsEventArgs> UpdateViewsEventHandler;
        private IList<ContextMenuItem> _methodsMenu;

        public IEnumerable<ContextMenuItem> MenuItems 
        { 
            get
            {
                if (_menuItems == null)
                {
                    var menuItems = createMenuItems();
                    if (menuItems != null && menuItems.Any())
                        _menuItems = menuItems;
                }
                return _menuItems;
            }
        }
        public OutlineItem Parent { get; set; }
        public ObservableCollection<OutlineItem> Children { get; set; }

        public OutlineItem()
        {
            Children = new ObservableCollection<OutlineItem>();
        }

        public void AddChild(OutlineItem child)
        {
            child.Parent = this;
            Children.Add(child);
        }
        public class ViewReference
        {
            public string ViewTypeName { get; set; }
            public Func<object> CodeElement { get; set; }
        }
        public void AddViews(IEnumerable<ViewReference> views)
        {
            _views = views;

        }

        private List<Method> _methodList;
        public class Method
        {
            public string Name { get; set; }
            public TextPoint StartPoint { get; set; }
            public int Priority { get; set; }
            public string Signature { get; set; }
            public string Comment { get; set; }
        }
        public void AddMethod(Method m)
        {
            if (_methodList == null) _methodList = new List<Method>();
            _methodList.Add(m);
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Create the context menus
        /// </summary>
        /// <returns></returns>
        private List<ContextMenuItem> createMenuItems()
        {
            var result=new List<ContextMenuItem>();

            //add the views
            if (_viewsMenu == null)
                _viewsMenu = createViewMenuItem();
            if (_viewsMenu != null)
                result.AddRange(_viewsMenu);

            //add the methods
            if (_methodsMenu == null)
                _methodsMenu = createMethodMenuItems();
            if (_methodsMenu != null)
            {
                if (result.Any()) result.Add(new ContextMenuItem() { Caption = "-" });
                result.AddRange(_methodsMenu);
            }

            return result;
        }

        /// <summary>
        /// Create the view part of the contextmenu
        /// </summary>
        /// <returns></returns>
        private IList<ContextMenuItem> createViewMenuItem()
        {
            // fire update view
            if (_views == null)
            {
                var handler = this.UpdateViewsEventHandler;
                if (handler != null) handler(this, new UpdateViewsEventArgs());
            }

            if (_views != null && _views.Any())
            {
                var result = new List<ContextMenuItem>();

                foreach(var view in _views)
                {
                    var contextMenuItem = new ContextMenuItem();

                    contextMenuItem.Caption = view.ViewTypeName;
                    contextMenuItem.Command = new GotoViewCommand((codeElement, lineNumber) => fireOpenProjectItem(view.CodeElement(), lineNumber), view.CodeElement, view.ViewTypeName);
                    contextMenuItem.ToolTipText = view.ViewTypeName;

                    result.Add(contextMenuItem);
                }
                return result;
            }
            return null;
        }
        

        private List<ContextMenuItem> createMethodMenuItems()
        {
            // create the method's menu
            if (_methodList != null && _methodList.Any())
            {
                var result = new List<ContextMenuItem>();
                // group the different method by name
                foreach (var methodGroup in _methodList.OrderBy(m => -m.Priority).OrderBy(m => m.StartPoint.Line).GroupBy(m => m.Name))
                {
                    // if mor the one method with same name, add paramters
                    if (methodGroup.Count() > 1)
                    {
                        foreach (var method in methodGroup)
                        {
                            var menuItem = new ContextMenuItem();
                            menuItem.Caption = method.Signature;
                            menuItem.ToolTipText = method.Comment;

                            menuItem.Command = new ContextMenuItemCommand(method.StartPoint, startPoint => fireContextMenuCommand(startPoint));
                            result.Add(menuItem);
                        }
                    }
                    else
                    {
                        var method = methodGroup.FirstOrDefault();
                        var menuItem = new ContextMenuItem();
                        menuItem.Caption = method.Name;
                        menuItem.ToolTipText = method.Comment;

                        menuItem.Command = new ContextMenuItemCommand(method.StartPoint, startPoint => fireContextMenuCommand(startPoint));
                        result.Add(menuItem);
                    }
                }
                return result;
            }
            return null;
        }

        private void fireContextMenuCommand(TextPoint startPoint)
        {
            var handler = this.ContextMenuItemEventHandler;

            if (handler != null)
                handler(this, new ContextMenuItemEventArgs(startPoint));
        }
        private void fireOpenProjectItem(object tgt, int linenumber)
        {


            var e = this.OpenProjectItemEventHandler;
            if (e != null)
            {
                e(this, new OpenProjectItemEventArgs(tgt, linenumber));
            }
        }

        public class ContextMenuItemEventArgs : EventArgs
        {
            public TextPoint StartPoint { get; private set; }

            public ContextMenuItemEventArgs(TextPoint startPoint)
            {
                StartPoint = startPoint;
            }
        }
        internal class ContextMenuItemCommand : ICommand
        {
            private TextPoint _startPoint;
            private Action<TextPoint> _onExecute;

            public event EventHandler CanExecuteChanged;

            public ContextMenuItemCommand(TextPoint StartPoint, Action<TextPoint> onExecute)
            {
                _startPoint = StartPoint;
                _onExecute = onExecute;
            }

            public bool CanExecute(object parameter)
            {
                return true;
            }

            public void Execute(object parameter)
            {
                try
                {
                    _onExecute(this._startPoint);
                }
                catch (Exception e)
                {

                }
            }
        }

        public class GotoCodeLocationEventArgs : EventArgs
        {
            public int LineNumber { get; private set; }

            public GotoCodeLocationEventArgs(int lineNumber)
            {
                LineNumber = lineNumber;
            }
        }

        public class OpenProjectItemEventArgs : GotoCodeLocationEventArgs
        {
            private readonly object _codeElement;

            public OpenProjectItemEventArgs(object codeElement, int lineNumber) : base(lineNumber)
            {
                _codeElement = codeElement;
            }

            public object CodeElement { get { return _codeElement; } }
        }

        public class GotoViewCommand : ICommand
        {
            public Action<object, int> OnExecute { get; set; }

            private readonly Func<object> _getCodeElementFunc;
            private readonly string _viewTypeName;

            public GotoViewCommand(Action<object, int> onExecute, Func<object> getCodeElementFunc, string viewTypeName)
            {
                OnExecute = onExecute;

                _getCodeElementFunc = getCodeElementFunc;
                _viewTypeName = viewTypeName;
            }

            public bool CanExecute(object parameter)
            {
                return true;
            }

            public void Execute(object parameter)
            {

                try
                {

                    var ce = (CodeElement)_getCodeElementFunc();
                    if (ce == null)
                    {
                        return;
                    }

                    OnExecute(ce, ce.StartPoint.Line);
                }
                catch (Exception e)
                {
                }
            }

            public event EventHandler CanExecuteChanged;
        }
        public class UpdateViewsEventArgs : EventArgs { }

    }
}
