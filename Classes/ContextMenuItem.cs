using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ClassOutline.Classes
{
    public class ContextMenuItem
    {
        public List<ContextMenuItem> MenuItems { get; set; }

        public ContextMenuItem()
        {
            MenuItems = new List<ContextMenuItem>();
        }
        public string Caption { get; set; }
        public ICommand Command { get; set; }
        public string ToolTipText { get; set; }
    }
}
