//
// DirectoryWalker.cs
//
// Copyright (C) 2005 Novell, Inc.
//

//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.
//

using System;
using System.Collections;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

using Mono.Unix.Native;

namespace Beagle.Util {

	public class DirectoryWalker {

		public  delegate bool   FileFilter      (string path, string name);
		private delegate object FileObjectifier (string path, string name);

		private static Encoding filename_encoding = Encoding.Default;

		private class FileEnumerator : IEnumerator {
		private string readdir ()
		{
			int r = 0;
			current_idx ++;
			if (current_idx >= entries.Length) {
				return null;
			}
			return entries[current_idx];
		}

			
			string path;
			FileFilter file_filter;
			FileObjectifier file_objectifier;
			string[] entries;
			int current_idx = -1;
			string current;

			public bool NamesOnly = false;
			
			public FileEnumerator (string          path,
					       FileFilter      file_filter,
					       FileObjectifier file_objectifier)
			{
				this.path = path;
				this.file_filter = file_filter;
				this.file_objectifier = file_objectifier;
				Reset ();
			}
			
			~FileEnumerator ()
			{
			}

			public object Current {
				get { 
					object current_obj = null;
					if (current != null) {
						if (file_objectifier != null)
							current_obj = file_objectifier (path, current); 
						else if (NamesOnly)
							current_obj = current;
						else
							current_obj = Path.Combine (path, current);
					}

					return current_obj;
				}
			}

			public bool MoveNext ()
			{
				bool skip_file = false;

				do {
					current = readdir ();
					if (current == null)
						break;

					skip_file = false;

					if (current == "." || current == "..") {
						skip_file = true;

					} else if (file_filter != null) {
						try {
							if (! file_filter (path, current))
								skip_file = true;

						} catch (Exception ex) {
							Logger.Log.Debug (ex, "Caught exception in file_filter");

							// If we have a filter that fails on a file,
							// it is probably safest to skip that file.
							skip_file = true;
						}
					}

				} while (skip_file);

				return current != null;
			}

			public void Reset ()
			{
				current = null;
				current_idx = -1;
				if (entries == null) {
					entries = Directory.GetFileSystemEntries(path);
				}
			}
		}

		private class FileEnumerable : IEnumerable {

			string path;
			FileFilter file_filter;
			FileObjectifier file_objectifier;
			
			public bool NamesOnly = false;

			public FileEnumerable (string          path,
					       FileFilter      file_filter,
					       FileObjectifier file_objectifier)
			{
				this.path = path;
				this.file_filter = file_filter;
				this.file_objectifier = file_objectifier;
			}

			public IEnumerator GetEnumerator ()
			{
				FileEnumerator e;
				e = new FileEnumerator (path, file_filter, file_objectifier);
				e.NamesOnly = this.NamesOnly;
				return e;
			}
		}

		static private bool IsFile (string path, string name)
		{
			return File.Exists (Path.Combine (path, name));
		}

		static private object FileInfoObjectifier (string path, string name)
		{
			return new FileInfo (Path.Combine (path, name));
		}

		/////////////////////////////////////////////////////////////////////////////////

		static public bool IsWalkable (string path)
		{
			return Directory.Exists(path);
		}

		/////////////////////////////////////////////////////////////////////////////////

		static public IEnumerable GetFiles (string path)
		{
			return new FileEnumerable (path, new FileFilter (IsFile), null);
		}

		static public IEnumerable GetFiles (DirectoryInfo dirinfo)
		{
			return GetFiles (dirinfo.FullName);
		}

		static public IEnumerable GetFileInfos (string path)
		{
			return new FileEnumerable (path,
						   new FileFilter (IsFile),
						   new FileObjectifier (FileInfoObjectifier));
		}

		static public IEnumerable GetFileInfos (DirectoryInfo dirinfo)
		{
			return GetFileInfos (dirinfo.FullName);
		}

		static private bool IsDirectory (string path, string name)
		{
			return Directory.Exists (Path.Combine (path, name));
		}

		static private object DirectoryInfoObjectifier (string path, string name)
		{
			return new DirectoryInfo (Path.Combine (path, name));
		}

		static public IEnumerable GetDirectories (string path)
		{
			return new FileEnumerable (path, new FileFilter (IsDirectory), null);
		}

		static public IEnumerable GetDirectories (DirectoryInfo dirinfo)
		{
			return GetDirectories (dirinfo.FullName);
		}

		static public IEnumerable GetDirectoryNames (string path)
		{
			FileEnumerable fe;
			fe = new FileEnumerable (path, new FileFilter (IsDirectory), null);
			fe.NamesOnly = true;
			return fe;
		}

		static public IEnumerable GetDirectoryInfos (string path)
		{
			return new FileEnumerable (path,
						   new FileFilter (IsDirectory),
						   new FileObjectifier (DirectoryInfoObjectifier));
		}

		static public IEnumerable GetDirectoryInfos (DirectoryInfo dirinfo)
		{
			return GetDirectoryInfos (dirinfo.FullName);
		}

		static public IEnumerable GetItems (string path, FileFilter filter)
		{
			return new FileEnumerable (path, filter, null);
		}

		static public IEnumerable GetItemNames (string path, FileFilter filter)
		{
			FileEnumerable fe;
			fe = new FileEnumerable (path, filter, null);
			fe.NamesOnly = true;
			return fe;
		}

		static public IEnumerable GetFileInfosRecursive (string path)
		{
			foreach (FileInfo i in DirectoryWalker.GetFileInfos (path))
				yield return i;

			foreach (string dir in DirectoryWalker.GetDirectories (path)) {
				foreach (FileInfo i in GetFileInfosRecursive (dir))
					yield return i;
			}

			yield break;
		}

		static public IEnumerable GetFileInfosRecursive (DirectoryInfo dirinfo)
		{
			return GetFileInfosRecursive (dirinfo.FullName);
		}

		static public int GetNumItems (string path)
		{
			int count = 0;
			FileFilter counting_filter = delegate (string dir, string name) {
							    count ++;
							    return false;
			};

			FileEnumerator dir_enum = new FileEnumerator (path, counting_filter, null);
			dir_enum.MoveNext ();
			return count;
		}

	}
}
