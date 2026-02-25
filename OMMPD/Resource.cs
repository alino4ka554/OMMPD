using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OMMPD
{
    public class Resource
    {
        public int Id { get; set; }
        public List<Operation> Operations { get; set; }
        public double ReleaseTime => (from operation in Operations select operation.EndTime).Max();
        public Resource(int id)
        {
            Id = id;
        }
    }
}
