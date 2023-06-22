using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace Editor
{
	class BuildProcessor : IPreprocessBuildWithReport
	{
		public int callbackOrder
		{
			get { return 0; }
		}

		public void OnPreprocessBuild(BuildReport report)
		{
			EditorUserBuildSettings.managedDebuggerFixedPort = 50000;
		}
	}
}
