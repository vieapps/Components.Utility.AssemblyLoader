using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyModel;
using Microsoft.Extensions.DependencyModel.Resolution;

#if !SIGN
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("VIEApps.Components.XUnitTests")]
#endif

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

			// have dependencies => load all
			if (File.Exists(Path.Combine(directory, $"{Path.GetFileNameWithoutExtension(assemblyFilePath)}.deps.json")))
			{
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

			// doesn't have dependencies => load referenced assembies
			else
				this.Assembly.GetReferencedAssemblies().Where(assemblyName => File.Exists(Path.Combine(directory, $"{assemblyName.Name}.dll")))
					.ToList().ForEach(assemblyName => this.AssemblyLoadContext.LoadFromAssemblyPath(Path.Combine(directory, $"{assemblyName.Name}.dll")));
		}

		/// <summary>
		/// Gets the type from an assembly by the specified name (full class name)
		/// </summary>
		/// <param name="assemblyFilePath">The absolute path of assembly</param>
		/// <param name="typeName">The type name (full class name)</param>
		/// <returns></returns>
		public static Type GetType(string assemblyFilePath, string typeName)
			=> string.IsNullOrWhiteSpace(assemblyFilePath) || string.IsNullOrWhiteSpace(typeName) || !File.Exists(assemblyFilePath)
				? null
				: new AssemblyLoader(assemblyFilePath).Assembly.GetExportedTypes().FirstOrDefault(type => typeName.Equals(type.ToString()));

		/// <summary>
		/// Gets the type by the specified type name (full class name with assembly)
		/// </summary>
		/// <param name="typeNameWithAssembly">The type name (full class name with assembly)</param>
		/// <returns></returns>
		public static Type GetType(string typeNameWithAssembly)
		{
			if (string.IsNullOrWhiteSpace(typeNameWithAssembly) || typeNameWithAssembly.IndexOf(",") < 0)
				return null;
			var type = Type.GetType(typeNameWithAssembly);
			if (type == null)
			{
				var info = typeNameWithAssembly.Trim().Split(',').Select(data => data.Trim()).ToList();
				type = AssemblyLoader.GetType(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{info[1]}{(info[1].ToLower().EndsWith(".dll") ? "" : ".dll")}"), info[0]);
			}
			return type;
		}
	}
}
