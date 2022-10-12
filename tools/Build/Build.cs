return BuildRunner.Execute(args, build =>
{
	var gitLogin = new GitLoginInfo("faithlifebuildbot", Environment.GetEnvironmentVariable("BUILD_BOT_PASSWORD") ?? "");

	build.AddDotNetTargets(
		new DotNetBuildSettings
		{
			NuGetApiKey = Environment.GetEnvironmentVariable("NUGET_API_KEY"),
			PackageSettings = new DotNetPackageSettings
			{
				GitLogin = gitLogin,
				PushTagOnPublish = x => $"v{x.Version}",
			},
		});
});
