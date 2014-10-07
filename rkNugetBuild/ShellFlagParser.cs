using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace rkNugetBuild {
  class ShellFlagParser {
    protected class FlagDictionary : DynamicObject {
      internal FlagDictionary(ShellFlagParser parser) {
        this.parser = parser;
      }

      protected ShellFlagParser parser;

      public override bool TryGetMember(GetMemberBinder binder, out object result) {
        result = parser.HasFlag(binder.Name);
        return true;
      }

      public override bool TryGetIndex(GetIndexBinder binder, object[] indexes, out object result) {
        result = parser.HasFlag((string)indexes[0]);
        return true;
      }

      public override bool TryInvoke(InvokeBinder binder, object[] args, out object result) {
        result = parser.HasFlag((string)args[0]);
        return true;
      }
    }

    protected class FollowerDictionary : DynamicObject {
      internal FollowerDictionary(ShellFlagParser parser) {
        this.parser = parser;
      }

      protected ShellFlagParser parser;

      public override bool TryGetMember(GetMemberBinder binder, out object result) {
        List<string> list;
        if(parser.FollowerList.TryGetValue(binder.Name, out list)) result = list;
        else result = null;
        return true;
      }

      protected bool TryGetFollower(object[] args, out object result) {
        if(args.Length != 2 || !(args[0] is string) || !(args[1] is int)) { result = null; return false; }
        string follower;
        if(parser.TryGetFollower((string)args[0], (int)args[1], out follower)) result = follower;
        else result = null;
        return true;
      }

      public override bool TryGetIndex(GetIndexBinder binder, object[] indexes, out object result) {
        return TryGetFollower(indexes, out result);
      }

      public override bool TryInvoke(InvokeBinder binder, object[] args, out object result) {
        return TryGetFollower(args, out result);
      }
    }

    public dynamic Flags { get; protected set; }

    public List<string> FlagList { get; protected set; }

    public List<string> NonFlags { get; protected set; }

    public dynamic Followers { get; protected set; }

    public Dictionary<string, List<string>> FollowerList { get; protected set; }

    public ShellFlagParser(string[] args, string[] flagConfig, Dictionary<string, int> followerConfig = null, string delim = "/") {
      Flags = new FlagDictionary(this);
      FlagList = new List<string>();
      NonFlags = new List<string>();
      Followers = new FollowerDictionary(this);
      FollowerList = new Dictionary<string, List<string>>();

      HashSet<string> flags = new HashSet<string>(flagConfig);
      flags.UnionWith(followerConfig.Keys);

      for(int i = 0; i < args.Length; i++) {
        string arg = args[i], flag = arg.Substring(delim.Length);
        if(arg.StartsWith(delim) && !FlagList.Contains(flag) && !String.IsNullOrEmpty(flag)) {
          if(!flags.Contains(flag)) throw new ShellArgumentException(arg, "Invalid flag name");

          int followers;
          if(followerConfig.TryGetValue(flag, out followers)) {
            for(int j = 1; j <= followers && j + i < args.Length; j++) {
              if(!FollowerList.ContainsKey(flag)) FollowerList.Add(flag, new List<string>());
              FollowerList[flag].Add(args[i + j]);
            }
            i += followers;
          }
          FlagList.Add(flag);
        }
        else {
          NonFlags.Add(arg);
        }
      }
    }

    public bool HasFlag(string flag) { return FlagList.Contains(flag); }

    public bool HasFollowers(string flag) { return FollowerList.ContainsKey(flag); }

    public bool TryGetFollower(string flag, int id, out string follower) {
      try {
        follower = this.FollowerList[flag][id];
        return true;
      }
      catch(Exception e) {
        if(!(e is KeyNotFoundException || e is ArgumentOutOfRangeException)) throw e;
        follower = null;
        return false;
      }
    }

    public void VerifyNonFlagCount(int required, int optional = 0) {
      int count = this.NonFlags.Count;
      VerifyNonFlagCountVariadic(required);
      if(count > (required + optional)) throw new ShellArgumentException(null, "Too many arguments");
    }

    public void VerifyNonFlagCountVariadic(int required) {
      if(this.NonFlags.Count < required)
        throw new ShellArgumentException(null, "Missing one or more required arguments");
    }
  }
}
