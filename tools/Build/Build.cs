using System;
using Faithlife.Build;

internal static class Build
{
	public static int Main(string[] args) => BuildRunner.Execute(args, build =>
	{
		build.AddDotNetTargets(
			new DotNetBuildSettings
			{
				NuGetApiKey = Environment.GetEnvironmentVariable("NUGET_API_KEY"),
				SourceLinkSettings = SourceLinkSettings.Default,
			});
	});
}
