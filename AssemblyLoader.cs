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
		/// <param name="assemblyPath">The full path to assembly</param>
		public AssemblyLoader(string assemblyPath)
		{
			// load assembly
			var path = Path.GetDirectoryName(assemblyPath);
			this.Assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath);
			this.AssemblyLoadContext = AssemblyLoadContext.GetLoadContext(this.Assembly);

			// not dependencies => load referenced assembies
			if (!File.Exists(Path.Combine(path, $"{Path.GetFileNameWithoutExtension(assemblyPath)}.deps.json")))
			{
				this.Assembly.GetReferencedAssemblies().ToList().ForEach(assemblyName => this.AssemblyLoadContext.LoadFromAssemblyPath(Path.Combine(path, $"{assemblyName.Name}.dll")));
				return;
			}

			this.DependencyContext = DependencyContext.Load(this.Assembly);
			this.CompilationAssemblyResolver = new CompositeCompilationAssemblyResolver(new ICompilationAssemblyResolver[]
			{
				new AppBaseCompilationAssemblyResolver(path),
				new ReferenceAssemblyPathResolver(),
				new PackageCompilationAssemblyResolver()
			});
			this.AssemblyLoadContext.Resolving += (assemblyLoadContext, assemblyName) =>
			{
				var runtimeLib = this.DependencyContext.RuntimeLibraries.FirstOrDefault(runtime => string.Equals(runtime.Name, assemblyName.Name, StringComparison.OrdinalIgnoreCase));
				if (runtimeLib != null)
				{
					var assemblyPaths = new List<string>();
					this.CompilationAssemblyResolver.TryResolveAssemblyPaths(new CompilationLibrary(
						runtimeLib.Type,
						runtimeLib.Name,
						runtimeLib.Version,
						runtimeLib.Hash,
						runtimeLib.RuntimeAssemblyGroups.SelectMany(g => g.AssetPaths),
						runtimeLib.Dependencies,
						runtimeLib.Serviceable
					), assemblyPaths);
					if (assemblyPaths.Count > 0)
						return assemblyLoadContext.LoadFromAssemblyPath(assemblyPaths[0]);
				}
				return null;
			};
		}
	}
}
