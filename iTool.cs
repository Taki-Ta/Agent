using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApp1.Tool
{
    public interface iTool
    {
        public string Name { get; }
        public string Description { get; }
        public object Parameters { get; }
        public string Execute(string arguments);
        public object GetSchema()
        {
            return new
            {
                type = "function",
                function = new
                {
                    name = this.Name,
                    description = this.Description,
                    parameters = this.Parameters
                }
            };
        }
    }
}
