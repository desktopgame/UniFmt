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
		DownloadAstyle();
		CreateDefaultSetting();
		AssetDatabase.Refresh();
	}

	private static void DownloadAstyle() {
		if(File.Exists(DOWNLOAD_ARCHIVE)) {
			return;
		}
		UnityEngine.Debug.Log("Download Astyle...");
		if(!Directory.Exists(DOWNLOAD_DIR)) {
			Directory.CreateDirectory(DOWNLOAD_DIR);
		}
		// download astyle
		var cli = new WebClient();
		byte[] data = cli.DownloadData("https://sourceforge.net/projects/astyle/files/latest/download");
		File.WriteAllBytes(DOWNLOAD_ARCHIVE, data);
		//unzip
		#if UNITY_STANDALONE_OSX || UNITY_STANDALONE_LINUX
		DoBashCommand($"tar -xzf {DOWNLOAD_ARCHIVE} -C {DOWNLOAD_DIR}");
		#else
		Tar.ExtractTarGz(DOWNLOAD_ARCHIVE, Application.dataPath + "/UniFmt");
		#endif
	}

	private static void CreateDefaultSetting() {
		if(File.Exists(FORMAT_SETTING)) {
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

	static void DoCrossPlatformCommand(string cmd) {
		#if UNITY_STANDALONE_OSX || UNITY_STANDALONE_LINUX
		DoBashCommand(cmd);
		#else
		DoDOSCommand(cmd);
		#endif
	}

	static void DoBashCommand(string cmd) {
		var p = new Process();
		p.StartInfo.FileName = "/bin/bash";
		p.StartInfo.Arguments = "-c \" " + cmd + " \"";
		p.StartInfo.UseShellExecute = false;
		p.StartInfo.RedirectStandardOutput = true;
		p.StartInfo.RedirectStandardError = true;
		p.Start();
		var output = p.StandardOutput.ReadToEnd();
		var error = p.StandardError.ReadToEnd();
		p.WaitForExit();
		p.Close();
		LogResult(output, error);
	}

	static void DoDOSCommand(string cmd) {
		var p = new Process();
		//p.StartInfo.FileName = "cmd.exe";
		p.StartInfo.FileName = System.Environment.GetEnvironmentVariable("ComSpec");
		p.StartInfo.Arguments = "/c \" " + cmd + " \"";
		p.StartInfo.UseShellExecute = false;
		p.StartInfo.RedirectStandardOutput = true;
		p.StartInfo.RedirectStandardError = true;
		p.Start();
		var output = p.StandardOutput.ReadToEnd();
		var error = p.StandardError.ReadToEnd();
		p.WaitForExit();
		p.Close();
		LogResult(output, error);
	}

	private static void LogResult(string output, string error) {
		if (output.Length > 0) {
			UnityEngine.Debug.Log(output);
		}

		if (error.Length > 0) {
			UnityEngine.Debug.LogError(error);
		}
	}
}
