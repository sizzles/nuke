// Copyright Matthias Koch 2017.
// Distributed under the MIT License.
// https://github.com/nuke-build/nuke/blob/master/LICENSE

using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using Nuke.Common.Tools.MSBuild;
using Nuke.Common.Tools.OpenCover;
using Nuke.Common.Tools.Xunit2;
using Nuke.Core.IO;
using Nuke.Core.Tooling;
using Xunit;

namespace Nuke.Common.Tests
{
    public class SettingsTest
    {
        private static PathConstruction.AbsolutePath RootDirectory
            => (PathConstruction.AbsolutePath) Directory.GetCurrentDirectory() / ".." / ".." / ".." / ".." / "..";

        private static void Assert<T> (Configure<T> configurator, string arguments)
            where T : ToolSettings, new()
        {
            configurator.Invoke(new T()).GetArguments().RenderForOutput().Should().Be(arguments);
        }

        [Fact]
        public void TestMSBuild ()
        {
            var projectFile = RootDirectory / "source" / "Nuke.Common" / "Nuke.Common.csproj";
            var solutionFile = RootDirectory / "Nuke.sln";

            Assert<MSBuildSettings>(x => x
                        .SetProjectFile(projectFile)
                        .SetTargetPlatform(MSBuildTargetPlatform.MSIL)
                        .SetConfiguration("Release")
                        .EnableNoLogo(),
                $"{projectFile} /nologo /property:Platform=AnyCPU /property:Configuration=Release");

            Assert<MSBuildSettings>(x => x
                        .SetProjectFile(solutionFile)
                        .SetTargetPlatform(MSBuildTargetPlatform.MSIL)
                        .ToggleRunCodeAnalysis(),
                $"{solutionFile} /property:Platform=\"Any CPU\" /property:RunCodeAnalysis=True");
        }

        [Fact]
        public void TestXunit2 ()
        {
            Assert<Xunit2Settings>(x => x
                        .AddTargetAssemblies("A.csproj")
                        .AddTargetAssemblyWithConfigs("B.csproj", "D.config", "new folder\\E.config"),
                "A.csproj  B.csproj D.config B.csproj \"new folder\\E.config\"");

            Assert<Xunit2Settings>(x => x
                        .SetResultPath("new folder\\data.xml"),
                "-Xml \"new folder\\data.xml\"");

            Assert<Xunit2Settings>(x => x
                        .SetResultPath("new folder\\data.xml", ResultFormat.HTML)
                        .EnableDiagnostics(),
                "-diagnostics -HTML \"new folder\\data.xml\"");
        }

        [Fact]
        public void TestOpenCover ()
        {
            var projectFile = RootDirectory / "source" / "Nuke.Common" / "Nuke.Common.csproj";

            Assert<OpenCoverSettings>(x => x
                        .SetTargetPath(projectFile)
                        .AddFilters("+[*]*", "-[xunit.*]*", "-[NUnit.*]*")
                        .SetTargetArguments("-diagnostics -HTML \"new folder\\data.xml\""),
                $"-target:{projectFile} -targetargs:\"-diagnostics -HTML \\\"new folder\\data.xml\\\"\" -filter:\"+[*]* -[xunit.*]* -[NUnit.*]*\"");
        }
    }
}
