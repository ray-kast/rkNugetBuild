using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace rkNugetBuild {
  class ShellArgumentException : Exception, ISerializable {
    public string Argument { get; protected set; }

    public ShellArgumentException() : base() { }

    public ShellArgumentException(string argument) : base(String.Format("Argument {0} invalid", argument)) { this.Argument = argument; }

    public ShellArgumentException(string argument, string message) : base(message) { this.Argument = argument; }

    public ShellArgumentException(string argument, Exception innerException) : base(String.Format("Argument {0} invalid", argument), innerException) { this.Argument = argument; }

    public ShellArgumentException(string argument, string message, Exception innerException) : base(message, innerException) { this.Argument = argument; }

    protected ShellArgumentException(SerializationInfo info, StreamingContext context) : base(info, context) { }
  }
}
