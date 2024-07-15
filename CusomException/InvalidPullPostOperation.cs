using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Promit.Test.CusomException
{
    public class InvalidPullPostOperation : Exception
    {
        public InvalidPullPostOperation(string message)
        : base(message) { }
    }
}
