#addin nuget:?package=Cake.FileHelpers&version=3.2.1
#addin nuget:?package=SharpZipLib&version=1.2.0
#addin nuget:?package=Cake.Compression&version=0.2.4
#addin nuget:?package=Cake.Json&version=4.0.0

// flags
var target = Argument("target", "Pack");
var buildEnabledOnly = Argument("build_enabled_only", false);

// filesystem
var binDir = Directory("bin");
var dasmodsDir = binDir + Directory("dasmods");
var dasmodsZip = binDir + File("dasmods.zip");

// config
var configFile = File("ror2.config.json");
var ror2Dir = "";
var enabledMods = new List<String>();

Task("InitConfig")
	.Does(() =>
{
	if (FileExists(configFile))
	{
		// noop
		Information($"Already exists: {configFile}");
	}
	else
	{
		// create a new file
		Information($"Creating: {configFile}");
		FileWriteText(File(configFile),
			SerializeJsonPretty(new Dictionary<string, object> {
				["ror2Dir"] = "",
				["enabledMods"] = new Dictionary<string, string>[] {}
			})
		);
	}
});

Task("ParseConfig")
	.IsDependentOn("InitConfig")
	.Does(() =>
{
	// sets outer scope vars
	Information($"Parsing {configFile}");
	var config = ParseJsonFromFile(configFile);
	ror2Dir = Directory((string)config["ror2Dir"]);
	enabledMods = ((JArray)config["enabledMods"]).Select(enabledMod => (string)enabledMod).ToList();
});

Task("Cleanup")
	.Does(() =>
{
	// clean bin and dasmods dirs
	Information("Removing old binaries");
	CreateDirectory(binDir);
	CleanDirectory(binDir);
	CreateDirectory(dasmodsDir);
	CleanDirectory(dasmodsDir);

	// clean last build objects
	Information("Cleaning up old build objects");
	CleanDirectories(GetDirectories("Mods/**/bin/"));
	CleanDirectories(GetDirectories("Mods/**/obj/"));
});

Task("Build")
	.IsDependentOn("ParseConfig")
	.IsDependentOn("Cleanup")
	.DoesForEach(GetFiles("Mods/**/*.sln"), (slnFile) =>
{
	// get modName
	var modName = slnFile.GetFilenameWithoutExtension();

	if (buildEnabledOnly && !enabledMods.Contains(modName.ToString()))
	{
		Information($"Skipping {modName}");
		return;
	}

	// build mod
	Information($"Building {modName}");
	MSBuild(slnFile, new MSBuildSettings { Configuration = "Release", Restore = true, Verbosity = Verbosity.Quiet });

	// copy mod to dasmods dir
	var dllDir = Directory($"./Mods/{modName}/bin/Release/netstandard2.0/");
	var dllFile = File($"{modName}.dll");
	Information($"Copying {dllFile} to {dasmodsDir}");
	CopyFile(dllDir + dllFile, dasmodsDir + dllFile);
});

Task("Pack")
	.IsDependentOn("Build")
	.Does(() =>
{
	// zip dasmods dir into dasmods.zip
	Information($"Zipping {dasmodsDir}");
	ZipCompress(dasmodsDir, dasmodsZip);
});

Task("InstallEnabledMods")
	.IsDependentOn("ParseConfig")
	.IsDependentOn("Pack")
	.Does(() =>
{
	// create and clean plugins dir
	var pluginsDir = Directory($"{ror2Dir}/BepInEx/plugins/dasmods");
	Information($"Cleaning {pluginsDir}");
	CreateDirectory(pluginsDir);
	CleanDirectory(pluginsDir);

	// copy enabled mods
	enabledMods.ForEach((modName) =>
	{
		var modFile = File($"{modName}.dll");
		Information($"Installing {modName}");
		CopyFile(dasmodsDir + modFile, pluginsDir + modFile);
	});
});

Task("Play")
	.IsDependentOn("InstallEnabledMods")
	.Does(() =>
{	
	// play ror2
	Information("Launching Risk of Rain 2");
	StartProcess($"{ror2Dir}/Risk of Rain 2");
});

RunTarget(target);