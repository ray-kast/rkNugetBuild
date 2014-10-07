using rkCommandLine;
using rkCommandLine.ShellArgs;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Media;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace rkNugetBuild {
  class Program {
    static void PauseQuit() {
      rkConsole.Pause("&W;Press any key to quit.");
    }

    static string MakeAbsolutePath(string root, string rel) {
      return Path.GetFullPath(Path.Combine(root, rel));
    }

    static ProcessStartInfo nugetStartInfo = new ProcessStartInfo("nuget") {
      UseShellExecute = false
    };

    static Process nuget = new Process() {
      StartInfo = nugetStartInfo
    };

    static bool RunNuget(string arguments) {
      try {
        nugetStartInfo.Arguments = arguments;
        rkConsole.PushColor('G');
        nuget.Start();
        nuget.WaitForExit();
        rkConsole.PopColor();
        if(nuget.ExitCode != 0) rkConsole.Write("&Y;Warning:&.; NuGet exited with code &W;{0}", nuget.ExitCode);
      }
      catch(Exception e) {
        SystemSounds.Hand.Play();
        rkConsole.WriteLine("&.;Error loading config files:&.; {0}", e.Message);
        return true;
      }
      return false;
    }

    static int Main(string[] args) {
      try {
        ShellFlagParser parser = new ShellFlagParser(args, new[] { "k", "p", "u", "v", "?" }, new Dictionary<string, int> { { "c", 1 }, { "i", 1 }, { "n", 1 }, { "o", 1 } });

        //Display help BEFORE any extra argument validation
        if(args.Length == 0 || parser.Flags["?"]) {
          rkConsole.WriteLine(@"&G;rk&W;NugetBuild&.; - Help

&W;Syntax:&.; rknugetbuild <Path>
  &W;Path:&.; Directory to build

&W;Flags:&.;
  &W;/c <path>:&.; Specify config file path; otherwise, ""\rkNugetBuild.xml""
  &W;/i <part>:&.; Increment part &W;<part>&.; of package version
    Possible values for &W;<part>&.;:
      &W;major:&.; Major version (1st part)
      &W;minor:&.; Minor version (2nd part)
      &W;rev:&.; Revision (3rd part)
      &W;build:&.; Build (4th part)
  &W;/k <key>:&.; Your Nuget API key
  &W;/n <path>:&.; Specify nuspec file path; otherwise, ""\Package.nuspec""
  &W;/o <path>:&.; Specify output directory; otherwise, ""..\PkgBuild""
  &W;/p:&.; Pause before exiting
  &W;/u:&.; Don't try to update NuGet
  &W;/v:&.; Prompt for NuGet package version number

&W;Version Syntax:&.; <Major>.<Minor>[.<Revision>[.<Build>]]
  &W;Major:&.; Should be incremented when a major feature update is released
  &W;Minor:&.; Should be incremented when a minor feature update is released
  &W;Revision:&.; Should be incremented when a bugfix update is released
  &W;Build:&.; Should be incremented every time the package is built
");
          if(parser.Flags.p) PauseQuit();
          return 0;
        }

        Assembly currAsm = Assembly.GetExecutingAssembly();
        AssemblyName name = new AssemblyName(currAsm.FullName);

        rkConsole.WriteLine("========== &G;rk&W;NugetBuild&.; v{0} ==========", name.Version.ToString(3));

        //Enforce number of non-flag arguments
        parser.VerifyNonFlagCount(1);

        string path = parser.NonFlags[0],
          configPath = MakeAbsolutePath(path, parser.Followers["c", 0] ?? @"rkNugetBuild.xml"),
          nuspecPath = MakeAbsolutePath(path, parser.Followers["n", 0] ?? @"Package.nuspec");

        XDocument configDoc, nuspecDoc;

        try {
          rkConsole.WriteLine("\nLoading config files...");
          configDoc = XDocument.Load(configPath);
          nuspecDoc = XDocument.Load(nuspecPath);
          rkConsole.WriteLine("Done!");
        }
        catch(Exception e) {
          SystemSounds.Hand.Play();
          rkConsole.WriteLine("&R;Error loading config files:&.; {0}", e.Message);
          if(parser.Flags.p) PauseQuit();
          return 1;
        }

        string versionString;

        try {
          versionString = nuspecDoc.Root.Element("metadata").Element("version").Value;
        }
        catch(NullReferenceException) {
          rkConsole.WriteLine("\n&R;Error loading package version");
          rkConsole.WriteLine("Please verify the nuspec file syntax is correct.");
          if(parser.Flags.p) PauseQuit();
          return 1;
        }

        Version ver;

        try {
          ver = Version.Parse(versionString);
        }
        catch(FormatException) {
          rkConsole.WriteLine("\n&R;Error parsing version string {0}", versionString);
          if(parser.Flags.p) PauseQuit();
          return 1;
        }

        if(parser.Flags.i && !parser.Flags.v) {
          string part;
          part = parser.Followers["i", 0];
          switch(part) {
            case "major":
              ver = new Version(ver.Major + 1, 0, 0, 0);
              break;
            case "minor":
              ver = new Version(ver.Major, ver.Minor + 1, 0, 0);
              break;
            case "rev":
              ver = new Version(ver.Major, ver.Minor, ver.Build + 1, 0);
              break;
            case "build":
              ver = new Version(ver.Major, ver.Minor, ver.Build, ver.Revision + 1);
              break;
            default:
              throw new ShellArgumentException(part, @"Acceptable values are ""major"", ""minor"", ""rev"", or ""build""");
          }
        }

        if(parser.Flags.v) {
          rkConsole.WriteLine("\nCurrent package version: &W;{0}", versionString);
          rkConsole.WriteLine("&W;Enter new version (or nothing to cancel)");

          string newVersionString;
          Version newVer = null;

          while(true) {
            rkConsole.Write("New version: ");
            rkConsole.PushColor('W');
            newVersionString = Console.ReadLine();
            rkConsole.PopColor();
            if(string.IsNullOrEmpty(newVersionString) || Version.TryParse(newVersionString, out newVer)) break;
            rkConsole.WriteLine("&Y;Invalid syntax");
          }

          ver = newVer ?? ver;
        }

        if(parser.Flags.i || parser.Flags.v) {
          //Write to file
        }

        rkConsole.WriteLine("\nPackage version is &W;{0}", ver.ToString());

        if(!parser.Flags.u) {
          rkConsole.WriteLine("\nUpdating NuGet...");

          if(RunNuget("update -self")) {
            if(parser.Flags.p) PauseQuit();
            return 1;
          }

          rkConsole.WriteLine("Done!");
        }

        if(parser.Flags.k) {
          rkConsole.WriteLine("\nSetting API key...");

          if(RunNuget("setapikey " + parser.Followers["k", 0])) {
            if(parser.Flags.p) PauseQuit();
            return 1;
          }
        }

        string tmpPath = "rkNuTmp";

        if(Directory.Exists(tmpPath)) {
          try {
            rkConsole.WriteLine("\nClearing temp directory...");
            Directory.Delete(tmpPath);
            rkConsole.WriteLine("Done!");
          }
          catch(IOException e) {
            rkConsole.WriteLine("&R;Error deleting temp directory:&.; {0}", e.Message);
            if(parser.Flags.p) PauseQuit();
            return 1;
          }
        }

        try {
          rkConsole.WriteLine("\nCreating temp directory...");
          Directory.CreateDirectory(tmpPath);
          rkConsole.WriteLine("Done!");
        }
        catch(IOException e) {
          rkConsole.WriteLine("&R;Error creating temp directory:&.; {0}", e.Message);
          if(parser.Flags.p) PauseQuit();
          return 1;
        }

        try {
          rkConsole.WriteLine("\nCleaning up temp directory...");
          Directory.Delete(tmpPath);
          rkConsole.WriteLine("Done!");
        }
        catch(IOException e) {
          rkConsole.WriteLine("&R;Error cleaning up temp directory:&.; {0}", e.Message);
          if(parser.Flags.p) PauseQuit();
          return 1;
        }

        if(parser.Flags.p) PauseQuit();
        return 0;
      }
      catch(ShellArgumentException e) {
        SystemSounds.Hand.Play();
        string sep = String.IsNullOrEmpty(e.Message) ? "." : ": ";
        if(e.Argument != null) rkConsole.WriteLine("&R;Argument \"{0}\" invalid{1}&.;{2}", e.Argument, sep, e.Message);
        else rkConsole.WriteLine("&R;Arguments invalid{1}&.;{2}", e.Argument, sep, e.Message);
        rkConsole.WriteLine("Try /? for help.");
        PauseQuit();
        return 1;
      }
#if !DEBUG
      catch(Exception e) {
        SystemSounds.Hand.Play();
        rkConsole.WriteLine("&R;Uncaught {0}", e.GetType().Name);
        if(!String.IsNullOrEmpty(e.Message)) rkConsole.WriteLine("&R;:&.; {0}", e.Message);
        PauseQuit();
        return 1;
      }
#endif
    }
  }
}
