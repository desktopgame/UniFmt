﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Linq;
using System.IO;
using System.Diagnostics;
using System.Threading;

/// <summary>
/// Astyle Format を Unityから利用するためのエディター拡張です。
/// Astyleについて(https://qiita.com/hakuta/items/29c988181d40829b1679)
/// ソースコードのフォーマットを統一するために追加しました。
/// </summary>
public class CodeFormat : EditorWindow {
	private List<string> files;
	private Vector2 scrollPos = Vector2.zero;
	private string searchText = "";
	private string astylePath;
	private bool maskDirectory;
	private string maskText;

	private static readonly string ASTYLE_PATH_KEY = "CodeFormat.AstylePath";
	private string FORMAT_SETTING;

	[MenuItem("Editor/CodeFormat")]
	static void CreateWindow() {
		CodeFormat window = (CodeFormat)EditorWindow.GetWindow(typeof(CodeFormat));
		window.Init();
		window.Show();
	}

	/// <summary>
	/// フォーマット対象となるファイルの一覧を取得します。
	/// </summary>
	private void Init() {
		this.FORMAT_SETTING = Application.dataPath + "/Editor/csfmt.txt";
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
			EditorGUILayout.LabelField(string.Format("{0}: not found", FORMAT_SETTING), FORMAT_SETTING);
			return;
		}

		this.scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
		EditorGUILayout.BeginVertical();

		if (GUILayout.Button("Update List")) {
			Init();
			return;
		}

		if (GUILayout.Button("Format All")) {
			FormatAll(files, "全てのファイルをフォーマットします。\nよろしいですか？");
			Close();
			return;
		}

		if (GUILayout.Button("Format All(Filtered)")) {
			FormatAll(GetFilteredFiles(), "フィルタされた全てのファイルをフォーマットします。\nよろしいですか？");
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

	/// <summary>
	/// 対象のファイルを全てフォーマットします。
	/// </summary>
	/// <param name="targetFiles">Target files.</param>
	/// <param name="message">Message.</param>
	private void FormatAll(List<string> targetFiles, string message) {
		bool result = EditorUtility.DisplayDialog(
						  "- CodeFormat -",
						  message,
						  "OK",
						  "取消し"
					  );

		if (!result) {
			return;
		}

		targetFiles.ForEach((e) => {
			RunFormat(e);
		});
		AssetDatabase.Refresh();
	}

	/// <summary>
	/// 検索バーを描画。
	/// </summary>
	private void ShowSearchBar() {
		EditorGUILayout.BeginHorizontal();
		EditorGUILayout.LabelField("search:");
		this.searchText = EditorGUILayout.TextField(searchText);
		EditorGUILayout.EndHorizontal();
	}

	/// <summary>
	/// ディレクトリでマスクするためのテキストフィールドを描画します。
	/// </summary>
	private void ShowMaskDirectory() {
		EditorGUILayout.BeginHorizontal();
		this.maskDirectory = GUILayout.Toggle(maskDirectory, "ディレクトリマスク");
		this.maskText = EditorGUILayout.TextField(maskText);
		EditorGUILayout.EndHorizontal();
	}

	/// <summary>
	/// Astyleの実行ファイルへのパスを設定するUI。
	/// </summary>
	private void ShowExecutableFileBar() {
		EditorGUILayout.BeginHorizontal();
		EditorGUILayout.LabelField("astyle path:");
		var temp = astylePath;
		//値が変更されていたので更新
		this.astylePath = EditorGUILayout.TextField(astylePath);

		if (temp != astylePath) {
			PlayerPrefs.SetString(ASTYLE_PATH_KEY, astylePath);
			PlayerPrefs.Save();
		}

		EditorGUILayout.EndHorizontal();
	}

	/// <summary>
	/// 指定のパスがマスクとマッチするなら true.
	/// </summary>
	/// <returns><c>true</c>, if mask was checked, <c>false</c> otherwise.</returns>
	/// <param name="pathname">Pathname.</param>
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

	/// <summary>
	/// フォーマット対象一覧の表示。
	/// </summary>
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

	/// <summary>
	/// 検索やディレクトリマスクによってフィルタされたパスの一覧を返します。
	/// </summary>
	/// <returns>The filtered files.</returns>
	private List<string> GetFilteredFiles() {
		var ret = new List<string>();

		foreach (var pathname in files) {
			var filename = Path.GetFileName(pathname);

			//マスクが有効でパスが含まれない
			if (!CheckMask(pathname)) {
				continue;
			}

			//検索テキストが含まれない
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
		#if UNITY_STANDALONE_OSX
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
