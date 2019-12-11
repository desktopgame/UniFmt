using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Linq;
using System.IO;
using System.Diagnostics;
using System.Threading;
using System.Net;

/// <summary>
/// UniFmt is editor extension of able to use program format of `astyle` from GUI.
/// in advance, need install `astyle` from http://astyle.sourceforge.net/
/// </summary>
public class UniFmtEditor : EditorWindow {
	private List<string> files;
	private Vector2 scrollPos = Vector2.zero;
	private string searchText = "";
	private string astylePath;
	private bool maskDirectory;
	private string maskText;

	private static readonly string ASTYLE_PATH_KEY = "UniFmt.AstylePath";
	private static string FORMAT_SETTING {
		get { return Application.dataPath + "/UniFmt/Editor/csfmt.txt"; }
	}
	private static string DOWNLOAD_DIR {
		get { return Application.dataPath + "/UniFmt/Cache/"; }
	}
	private static string DOWNLOAD_ARCHIVE {
		get {
			#if UNITY_STANDALONE_OSX || UNITY_STANDALONE_LINUX
			return DOWNLOAD_DIR + "data.tar.gz";
			#else
			return DOWNLOAD_DIR + "data.zip";
			#endif
		}
	}
	private static string LOCK_FILE {
		get { return Application.dataPath + "/UniFmt/Cache/lock.txt"; }
	}
	private static string DOWNLOAD_URL {
		get {
			#if UNITY_STANDALONE_OSX || UNITY_STANDALONE_LINUX
			return "https://sourceforge.net/projects/astyle/files/latest/download";
			#else
			return "https://sourceforge.net/projects/astyle/files/astyle/astyle%203.1/AStyle_3.1_windows.zip/download";
			#endif
		}
	}

	[MenuItem("Assets/UniFmt/Help")]
	static void ShowHelp() {
		EditorUtility.DisplayDialog(
			"- UniFmt -",
			"please install a astyle from following url: http://astyle.sourceforge.net/",
			"OK",
			"Cancel"
		);
	}

	[MenuItem("Assets/UniFmt/Setup")]
	static void Setup() {
		if(Directory.Exists(Path.GetDirectoryName(LOCK_FILE)) && File.Exists(LOCK_FILE)) {
			UnityEngine.Debug.Log("please remove UniFmt/Cache/lock.txt if execute setup");
			return;
		}
		DownloadAstyle();
		CreateDefaultSetting();
		#if UNITY_STANDALONE_OSX || UNITY_STANDALONE_LINUX
		File.Create(LOCK_FILE);
		#endif
		AssetDatabase.Refresh();
	}

	private static void DownloadAstyle() {
		UnityEngine.Debug.Log("Download Astyle...");
		if (!Directory.Exists(DOWNLOAD_DIR)) {
			Directory.CreateDirectory(DOWNLOAD_DIR);
		}
		// download astyle
		var cli = new WebClient();
		byte[] data = cli.DownloadData(DOWNLOAD_URL);
		File.WriteAllBytes(DOWNLOAD_ARCHIVE, data);
		//unzip
		#if UNITY_STANDALONE_OSX || UNITY_STANDALONE_LINUX
		DoBashCommand($"tar -xzf {DOWNLOAD_ARCHIVE} -C {DOWNLOAD_DIR}");
		RunCMake($"{DOWNLOAD_DIR}astyle");
		#else
		PlayerPrefs.SetString(ASTYLE_PATH_KEY, DOWNLOAD_DIR + "astyle/bin/astyle.exe");
		PlayerPrefs.Save();
		// show dialog
		EditorUtility.DisplayDialog(
			"- UniFmt -",
			"please unzip `data.zip`",
			"OK",
			"Cancel"
		);
		var openPath = Application.dataPath + "/UniFmt/Cache";
		Process.Start("explorer", $"\"{openPath}\"");
		#endif
	}

	private static void RunCMake(string sourceDir) {
		DoBashCommand($"/usr/local/bin/cmake {sourceDir}");
		var env = System.Environment.CurrentDirectory;
		DoBashCommand($"/usr/bin/make");
		Directory.Move(env + "/CMakeFiles", DOWNLOAD_DIR + "astyle/CMakeFiles");
		File.Move(env + "/CMakeCache.txt", DOWNLOAD_DIR + "astyle/CMakeCache.txt");
		File.Move(env + "/Makefile", DOWNLOAD_DIR + "astyle/Makefile");
		File.Move(env + "/cmake_install.cmake", DOWNLOAD_DIR + "astyle/cmake_install.cmake");
		File.Move(env + "/astyle", DOWNLOAD_DIR + "astyle/astyle");
		var gitignore = Application.dataPath + "/UniFmt/.gitignore";
		if(!File.Exists(gitignore)) {
			File.WriteAllText(gitignore, "Cache\nCache.meta");
		}
		PlayerPrefs.SetString(ASTYLE_PATH_KEY, DOWNLOAD_DIR + "astyle/astyle");
		PlayerPrefs.Save();
	}

	private static void CreateDefaultSetting() {
		if (File.Exists(FORMAT_SETTING)) {
			return;
		}

		var strBuf = new System.Text.StringBuilder();
		strBuf.AppendLine("#");
		strBuf.AppendLine("# CodeFormat.cs で使用されます。");
		strBuf.AppendLine("#");
		strBuf.AppendLine("");
		strBuf.AppendLine("# c#のファイルとして認識する");
		strBuf.AppendLine("mode=cs");
		strBuf.AppendLine("# allmanスタイルにする");
		strBuf.AppendLine("style=java");
		strBuf.AppendLine("# インデントにタブを使う");
		strBuf.AppendLine("indent=tab=4");
		strBuf.AppendLine("# 継続行のインデントにもタブを使う(但し偶数個のタブでないときはwhite spaceで埋められる)");
		strBuf.AppendLine("indent=force-tab=4");
		strBuf.AppendLine("# namespace文の中をインデントする");
		strBuf.AppendLine("indent-namespaces");
		strBuf.AppendLine("# switch文の中をインデントする");
		strBuf.AppendLine("indent-switches");
		strBuf.AppendLine("# case文の中をインデントする");
		strBuf.AppendLine("indent-cases");
		strBuf.AppendLine("# 1行ブロックを許可する");
		strBuf.AppendLine("keep-one-line-blocks");
		strBuf.AppendLine("# 1行文を許可する");
		strBuf.AppendLine("keep-one-line-statements");
		strBuf.AppendLine("# プリプロセッサをソースコード内のインデントと合わせる");
		strBuf.AppendLine("indent-preproc-cond");
		strBuf.AppendLine("# if, while, switchの後にpaddingを入れる");
		strBuf.AppendLine("pad-header");
		strBuf.AppendLine("# 演算子の前後にpaddingを入れる");
		strBuf.AppendLine("pad-oper");
		strBuf.AppendLine("# originalファイルを生成しない");
		strBuf.AppendLine("suffix=none");
		strBuf.AppendLine("# if, for, while文の前後に空行を入れる");
		strBuf.AppendLine("break-blocks");
		strBuf.AppendLine("# コメントもインデントする");
		strBuf.AppendLine("indent-col1-comments");
		strBuf.AppendLine("#カンマの間にスペース");
		strBuf.AppendLine("pad-comma");
		File.WriteAllText(FORMAT_SETTING, strBuf.ToString());
	}

	[MenuItem("Assets/UniFmt/Format")]
	static void CreateWindow() {
		UniFmtEditor window = (UniFmtEditor)EditorWindow.GetWindow(typeof(UniFmtEditor));
		window.Init();
		window.Show();
	}

	private void Init() {
		this.files = new List<string>();
		this.astylePath = PlayerPrefs.GetString(ASTYLE_PATH_KEY, "astyle");
		var assets = Application.dataPath;
		var all = Directory.GetFiles(assets, "*.cs", SearchOption.AllDirectories);
		files = all.OrderBy(f => File.GetLastWriteTime(f))
				.Reverse()
				.ToList();
	}

	/// <summary>
	/// ファイルをフォーマットするためのGUIを描画。
	/// </summary>
	void OnGUI() {
		if (EditorApplication.isCompiling || Application.isPlaying) {
			return;
		}

		if (!File.Exists(FORMAT_SETTING)) {
			EditorGUILayout.LabelField(string.Format("{0}: No such file.", FORMAT_SETTING));
			return;
		}

		this.scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
		EditorGUILayout.BeginVertical();

		if (GUILayout.Button("Update List")) {
			Init();
			return;
		}

		if (GUILayout.Button("Format All")) {
			FormatAll(files, "format a all files.\nit is ok?");
			Close();
			return;
		}

		if (GUILayout.Button("Format All(Filtered)")) {
			FormatAll(GetFilteredFiles(), "format a filtered all files.\nit is ok?");
			Close();
			return;
		}

		ShowExecutableFileBar();
		ShowMaskDirectory();
		ShowSearchBar();
		ShowFileList();
		EditorGUILayout.EndVertical();
		EditorGUILayout.EndScrollView();
	}

	private void FormatAll(List<string> targetFiles, string message) {
		bool result = EditorUtility.DisplayDialog(
						  "- UniFmt -",
						  message,
						  "OK",
						  "Cancel"
					  );

		if (!result) {
			return;
		}

		targetFiles.ForEach((e) => {
			RunFormat(e);
		});
		AssetDatabase.Refresh();
	}

	private void ShowSearchBar() {
		EditorGUILayout.BeginHorizontal();
		EditorGUILayout.LabelField("Search:");
		this.searchText = EditorGUILayout.TextField(searchText);
		EditorGUILayout.EndHorizontal();
	}

	private void ShowMaskDirectory() {
		EditorGUILayout.BeginHorizontal();
		this.maskDirectory = GUILayout.Toggle(maskDirectory, "DirectoryMask");
		this.maskText = EditorGUILayout.TextField(maskText);
		EditorGUILayout.EndHorizontal();
	}

	private void ShowExecutableFileBar() {
		EditorGUILayout.BeginHorizontal();
		EditorGUILayout.LabelField("astyle Path(File):");
		var temp = astylePath;
		//値が変更されていたので更新
		this.astylePath = EditorGUILayout.TextField(astylePath);

		if (temp != astylePath) {
			PlayerPrefs.SetString(ASTYLE_PATH_KEY, astylePath);
			PlayerPrefs.Save();
		}

		EditorGUILayout.EndHorizontal();
	}

	private bool CheckMask(string pathname) {
		if (!maskDirectory) {
			return true;
		}

		if (maskText == "" || maskText == null) {
			return true;
		}

		var dirname = Path.GetDirectoryName(pathname);
		return maskDirectory && dirname.Contains(maskText);
	}

	private void ShowFileList() {
		foreach (var pathname in GetFilteredFiles()) {
			var filename = Path.GetFileName(pathname);
			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.LabelField(filename);

			if (GUILayout.Button("Format")) {
				RunFormat(pathname);
			}

			EditorGUILayout.EndHorizontal();
		}
	}

	private List<string> GetFilteredFiles() {
		var ret = new List<string>();

		foreach (var pathname in files) {
			var filename = Path.GetFileName(pathname);

			if (!CheckMask(pathname)) {
				continue;
			}

			if (searchText.Length > 0 && !filename.Contains(searchText)) {
				continue;
			}

			ret.Add(pathname);
		}

		return ret;
	}

	private void RunFormat(string filename) {
		try {
			var cmd = string.Format(astylePath + " --options={0} {1}", FORMAT_SETTING, filename);
			UnityEngine.Debug.Log(cmd);
			DoCrossPlatformCommand(cmd);
		} catch (System.Exception e) {
			UnityEngine.Debug.LogError(e);
		}
	}

	//
	// Shell Utility
	//

	static void DoCrossPlatformCommand(string cmd) {
		#if UNITY_STANDALONE_OSX || UNITY_STANDALONE_LINUX
		DoBashCommand(cmd);
		#else
		DoDOSCommand(cmd);
		#endif
	}

	static void DoBashCommand(string cmd) {
		ShellExec(CreateBashProcess(cmd));
	}

	private static Process CreateBashProcess(string cmd) {
		var p = new Process();
		p.StartInfo.FileName = "/bin/bash";
		p.StartInfo.Arguments = "-c \" " + cmd + " \"";
		p.StartInfo.UseShellExecute = false;
		p.StartInfo.RedirectStandardOutput = true;
		p.StartInfo.RedirectStandardError = true;
		return p;
	}

	static void DoDOSCommand(string cmd) {
		ShellExec(CreateDOSProcess(cmd));
	}

	private static Process CreateDOSProcess(string cmd) {
		var p = new Process();
		p.StartInfo.FileName = System.Environment.GetEnvironmentVariable("ComSpec");
		p.StartInfo.Arguments = "/c \" " + cmd + " \"";
		p.StartInfo.UseShellExecute = false;
		p.StartInfo.RedirectStandardOutput = true;
		p.StartInfo.RedirectStandardError = true;
		return p;
	}

	private static void ShellExec(Process p) {
		ShellExecGet(p, out var output, out var error);
		ShowResult(output, error);
	}

	private static void ShellExecGet(Process p, out string output, out string error) {
		p.Start();
		output = p.StandardOutput.ReadToEnd();
		error = p.StandardError.ReadToEnd();
		p.WaitForExit();
		p.Close();
	}

	private static void ShowResult(string output, string error) {
		if (output.Length > 0) {
			UnityEngine.Debug.Log(output);
		}

		if (error.Length > 0) {
			UnityEngine.Debug.LogError(error);
		}
	}
}
