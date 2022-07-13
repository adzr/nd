//////////////////////////////////////////////////////////////////////
// Variables
//////////////////////////////////////////////////////////////////////

var target = Argument("target", "Test");
var configuration = Argument("configuration", "Release");
var sln = Argument("sln", "./nd.sln");
var includeIntegration = Argument<bool>("integration", false);

//////////////////////////////////////////////////////////////////////
// TASKS
//////////////////////////////////////////////////////////////////////

Task("Validate")
    .Does(() =>
{
    Information("Nothing to do here.");
});

Task("Clean")
    .IsDependentOn("Validate")
    .WithCriteria(c => HasArgument("rebuild"))
    .DoesForEach(GetDirectories("./src/*/*"), (parent) =>
{
    var directoryObj = $"{parent}/obj/{configuration}";
    var directoryBin = $"{parent}/bin/{configuration}";
    
    Information("Cleaning directory: {0}", directoryObj);
    CleanDirectory(directoryObj);
    
    Information("Cleaning directory: {0}", directoryBin);
    CleanDirectory(directoryBin);
});

Task("Restore")
    .IsDependentOn("Clean")
    .WithCriteria(c => HasArgument("rebuild") || HasArgument("restore"))
    .Does(() =>
{
    Information("Restoring dependencies...");
    DotNetRestore(sln);
});

Task("Build")
    .IsDependentOn("Restore")
    .Does(() =>
{
    DotNetBuild(sln, new DotNetCoreBuildSettings
    {
        Configuration = configuration,
    });
});

Task("Test")
    .IsDependentOn("Build")
    .Does(() =>
{
    DotNetTest(sln, new DotNetCoreTestSettings
    {
        Configuration = configuration,
        NoBuild = true,
        Filter = "Category=UnitTest" +
          (includeIntegration ? "|Category=IntegrationTest" : "")
    });
});

Task("Pack")
    .IsDependentOn("Test")
    .Does(() =>
{
    Information("Nothing to do here.");
});

//////////////////////////////////////////////////////////////////////
// EXECUTION
///////////////////////////////////////////	///////////////////////////

RunTarget(target);
