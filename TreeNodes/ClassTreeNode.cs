using EnvDTE;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClassOutline.TreeNodes
{
    public class ClassTreeNode : GenericTreeNode<CodeClass>
    {

        public IList<string> BaseClassList { get; private set; }
        public ClassTreeNode(CodeClass source) : base(source)
        {
            try
            {
                FullName = source.FullName;
                Name = source.FullName.Split('.').Last();
                Kind = "Classes";
                Access = source.Access;

                BaseClassList = AddBaseClasses(source);
            }
            catch (Exception e)
            {
                Debug.WriteLine("Failed to create ClassTreeNode", e);
                throw;
            }
        }

        private IList<string> AddBaseClasses(CodeClass source)
        {
            var list = new List<string>();

            foreach (CodeElement codeElement in source.Bases)
            {
                list.Add(codeElement.FullName);
                var baseClass = codeElement as CodeClass;
                if (baseClass != null)
                {
                    list.AddRange(AddBaseClasses(baseClass));
                }
            }

            return list;
        }
        public Type GetBaseType()
        {

            var baseClasse = BaseClassList.FirstOrDefault();

            if (baseClasse == null) return null;

            try
            {
                var type = Type.GetType(baseClasse, true, true);
                return type;
            }
            catch (Exception e)
            {
                Debug.WriteLine("Failed to create type : " + baseClasse + ". " + e.Message);
            }
            return null;
        }

        public string GetBaseTypeName()
        {
            return BaseClassList.FirstOrDefault();
        }
    }
}
