namespace VirtoCommerce.SalesRep.Tests.Infrastructure;

/// <summary>
/// What a harness hosts beyond the modules every deployment of this module has: whether the optional task management
/// module is installed, and the folder the file system blob provider keeps the assets in. (The other optional module,
/// Google Analytics, is hosted by the stand-in for Google's API when a test registers it: <c>TestAnalytics</c>.)
/// </summary>
/// <param name="TaskManagement">VirtoCommerce.TaskManagement: the tasks and calls. Without it, the module list doesn't name it either.</param>
/// <param name="AssetsRootPath">The assets' folder: one per harness, so a test sees only its own files.</param>
internal sealed record HostedModules(bool TaskManagement, string AssetsRootPath);
