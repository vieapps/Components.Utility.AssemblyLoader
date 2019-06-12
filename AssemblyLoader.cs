using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyModel;
using Microsoft.Extensions.DependencyModel.Resolution;

namespace net.vieapps.Components.Utility
{
	public class AssemblyLoader
	{
		/// <summary>
		/// Gets the loaded assembly
		/// </summary>
		public Assembly Assembly { get; }

		AssemblyLoadContext AssemblyLoadContext { get; }

		DependencyContext DependencyContext { get; }

		ICompilationAssemblyResolver CompilationAssemblyResolver { get; }

		/// <summary>
		/// Creates new instance to dynamic load an assembly
		/// </summary>
		/// <param name="assemblyFilePath">The full path to assembly</param>
		public AssemblyLoader(string assemblyFilePath)
		{
			// load assembly
			var directory = Path.GetDirectoryName(assemblyFilePath);
			this.Assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyFilePath);
			this.AssemblyLoadContext = AssemblyLoadContext.GetLoadContext(this.Assembly);

			// not dependencies => load referenced assembies
			if (!File.Exists(Path.Combine(directory, $"{Path.GetFileNameWithoutExtension(assemblyFilePath)}.deps.json")))
			{
				this.Assembly.GetReferencedAssemblies().ToList().ForEach(assemblyName => this.AssemblyLoadContext.LoadFromAssemblyPath(Path.Combine(directory, $"{assemblyName.Name}.dll")));
				return;
			}

			this.DependencyContext = DependencyContext.Load(this.Assembly);
			this.CompilationAssemblyResolver = new CompositeCompilationAssemblyResolver(new ICompilationAssemblyResolver[]
			{
				new AppBaseCompilationAssemblyResolver(directory),
				new ReferenceAssemblyPathResolver(),
				new PackageCompilationAssemblyResolver()
			});
			this.AssemblyLoadContext.Resolving += (assemblyLoadContext, assemblyName) =>
			{
				var runtime = this.DependencyContext.RuntimeLibraries.FirstOrDefault(rtLib => string.Equals(rtLib.Name, assemblyName.Name, StringComparison.OrdinalIgnoreCase));
				if (runtime != null)
				{
					var assemblyPaths = new List<string>();
					this.CompilationAssemblyResolver.TryResolveAssemblyPaths(new CompilationLibrary(
						runtime.Type,
						runtime.Name,
						runtime.Version,
						runtime.Hash,
						runtime.RuntimeAssemblyGroups.SelectMany(g => g.AssetPaths),
						runtime.Dependencies,
						runtime.Serviceable
					), assemblyPaths);
					if (assemblyPaths.Count > 0)
						return assemblyLoadContext.LoadFromAssemblyPath(assemblyPaths[0]);
				}
				return null;
			};
		}
	}
}
