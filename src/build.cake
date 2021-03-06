
//////////////////////////////////////////////////////////////////////
// TOOLS / ADDINS
//////////////////////////////////////////////////////////////////////

#tool paket:?package=GitVersion.CommandLine
#tool paket:?package=gitreleasemanager
#addin paket:?package=Cake.Figlet
#addin paket:?package=Cake.Paket

//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////

var target = Argument("target", "Default");
if (string.IsNullOrWhiteSpace(target))
{
    target = "Default";
}

var configuration = Argument("configuration", "Release");
if (string.IsNullOrWhiteSpace(configuration))
{
    configuration = "Release";
}

//////////////////////////////////////////////////////////////////////
// PREPARATION
//////////////////////////////////////////////////////////////////////

// Set build version
GitVersion(new GitVersionSettings { OutputType = GitVersionOutput.BuildServer });
GitVersion gitVersion;

var local = BuildSystem.IsLocalBuild;
var isPullRequest = AppVeyor.Environment.PullRequest.IsPullRequest;
var isDevelopBranch = StringComparer.OrdinalIgnoreCase.Equals("dev", AppVeyor.Environment.Repository.Branch);
var isReleaseBranch = StringComparer.OrdinalIgnoreCase.Equals("master", AppVeyor.Environment.Repository.Branch);
var isTagged = AppVeyor.Environment.Repository.Tag.IsTag;

// Define directories.
var buildDir = "./bin";
var publishDir = "./Publish";

//////////////////////////////////////////////////////////////////////
// TASKS
//////////////////////////////////////////////////////////////////////

Setup(context =>
{
    gitVersion = GitVersion(new GitVersionSettings { OutputType = GitVersionOutput.Json });
    Information("Informational Version  : {0}", gitVersion.InformationalVersion);
    Information("SemVer Version         : {0}", gitVersion.SemVer);
    Information("AssemblySemVer Version : {0}", gitVersion.AssemblySemVer);
    Information("MajorMinorPatch Version: {0}", gitVersion.MajorMinorPatch);
    Information("NuGet Version          : {0}", gitVersion.NuGetVersion);
    Information("IsLocalBuild           : {0}", local);

    Information(Figlet("MahApps.Metro.SimpleChildWindow"));
});

Task("Clean")
    .Does(() =>
{
    CleanDirectory(Directory(buildDir));
    CleanDirectory(Directory(publishDir));
});

Task("Paket-Restore")
    .IsDependentOn("Clean")
    .Does(() =>
{
    PaketRestore();
});

Task("Update-SolutionInfo")
    .Does(() =>
{
	var solutionInfo = "./MahApps.Metro.SimpleChildWindow/Properties/AssemblyInfo.cs";
	GitVersion(new GitVersionSettings { UpdateAssemblyInfo = true, UpdateAssemblyInfoFilePath = solutionInfo});
});

Task("Build")
    .IsDependentOn("Paket-Restore")
    .Does(() =>
{
    if(IsRunningOnWindows())
    {
      // Use MSBuild
      MSBuild("./MahApps.Metro.SimpleChildWindow.sln", settings => settings.SetMaxCpuCount(0).SetConfiguration(configuration));
    }
});

Task("Paket-Pack")
    .WithCriteria(() => !isPullRequest)
    .Does(() =>
{
	EnsureDirectoryExists(Directory(publishDir));
	PaketPack(publishDir, new PaketPackSettings { Version = isReleaseBranch ? gitVersion.MajorMinorPatch : gitVersion.NuGetVersion });
});

Task("Zip-Demos")
    .WithCriteria(() => !isPullRequest)
    .Does(() =>
{
	EnsureDirectoryExists(Directory(publishDir));
    Zip(buildDir + "/Release/MahApps.Metro.SimpleChildWindow.Demo/", publishDir + "/MahApps.Metro.SimpleChildWindow.Demo-v" + gitVersion.NuGetVersion + ".zip");
});

Task("CreateRelease")
    .WithCriteria(() => !isTagged)
    .Does(() =>
{
    var username = EnvironmentVariable("GITHUB_USERNAME");
    if (string.IsNullOrEmpty(username))
    {
        throw new Exception("The GITHUB_USERNAME environment variable is not defined.");
    }

    var token = EnvironmentVariable("GITHUB_TOKEN");
    if (string.IsNullOrEmpty(token))
    {
        throw new Exception("The GITHUB_TOKEN environment variable is not defined.");
    }

    GitReleaseManagerCreate(username, token, "punker76", "MahApps.Metro.SimpleChildWindow", new GitReleaseManagerCreateSettings {
        Milestone         = gitVersion.MajorMinorPatch,
        Name              = gitVersion.MajorMinorPatch,
        Prerelease        = false,
        TargetCommitish   = "master",
        WorkingDirectory  = "../"
    });
});

Task("ExportReleaseNotes")
    .Does(() =>
{
    var username = EnvironmentVariable("GITHUB_USERNAME");
    if (string.IsNullOrEmpty(username))
    {
        throw new Exception("The GITHUB_USERNAME environment variable is not defined.");
    }

    var token = EnvironmentVariable("GITHUB_TOKEN");
    if (string.IsNullOrEmpty(token))
    {
        throw new Exception("The GITHUB_TOKEN environment variable is not defined.");
    }

    EnsureDirectoryExists(Directory(publishDir));
    GitReleaseManagerExport(username, token, "punker76", "MahApps.Metro.SimpleChildWindow", publishDir + "/releasenotes.md", new GitReleaseManagerExportSettings {
        // TagName         = gitVersion.SemVer,
        TagName         = "1.5.0",
        TargetDirectory = publishDir,
        LogFilePath     = publishDir + "/grm.log"
    });
});

//////////////////////////////////////////////////////////////////////
// TASK TARGETS
//////////////////////////////////////////////////////////////////////

Task("Default")
    .IsDependentOn("Build");

Task("appveyor")
    .IsDependentOn("Update-SolutionInfo")
    .IsDependentOn("Build")
    .IsDependentOn("Paket-Pack")
    .IsDependentOn("Zip-Demos");

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////

RunTarget(target);
