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

		AssemblyLoadContext LoadContext { get; }

		DependencyContext DependencyContext { get; }

		ICompilationAssemblyResolver AssemblyResolver { get; }

		/// <summary>
		/// Creates new instance of assembly loader
		/// </summary>
		/// <param name="assemblyPath">The full path to assembly</param>
		public AssemblyLoader(string assemblyPath)
		{
			var path = Path.GetDirectoryName(assemblyPath);
			this.Assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath);
			this.LoadContext = AssemblyLoadContext.GetLoadContext(this.Assembly);
			if (File.Exists(Path.Combine(path, $"{Path.GetFileNameWithoutExtension(assemblyPath)}.deps.json")))
			{
				this.DependencyContext = DependencyContext.Load(this.Assembly);
				this.AssemblyResolver = new CompositeCompilationAssemblyResolver(new ICompilationAssemblyResolver[]
				{
					new AppBaseCompilationAssemblyResolver(path),
					new ReferenceAssemblyPathResolver(),
					new PackageCompilationAssemblyResolver()
				});
				this.LoadContext.Resolving += (context, name) =>
				{
					var runtimeLib = this.DependencyContext.RuntimeLibraries.FirstOrDefault(runtime => string.Equals(runtime.Name, name.Name, StringComparison.OrdinalIgnoreCase));
					if (runtimeLib != null)
					{
						var compilationLib = new CompilationLibrary(
							runtimeLib.Type,
							runtimeLib.Name,
							runtimeLib.Version,
							runtimeLib.Hash,
							runtimeLib.RuntimeAssemblyGroups.SelectMany(g => g.AssetPaths),
							runtimeLib.Dependencies,
							runtimeLib.Serviceable
						);
						var assemblyPaths = new List<string>();
						this.AssemblyResolver.TryResolveAssemblyPaths(compilationLib, assemblyPaths);
						if (assemblyPaths.Count > 0)
							return this.LoadContext.LoadFromAssemblyPath(assemblyPaths[0]);
					}
					return null;
				};
			}
			else
				this.Assembly.GetReferencedAssemblies().ToList().ForEach(name => this.LoadContext.LoadFromAssemblyPath(Path.Combine(path, $"{name.Name}.dll")));
		}
	}
}
