﻿using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Hacknet;
using Pathfinder.Util;

namespace Pathfinder.GameFilesystem
{
    public class Directory : FileObject<Folder, IFileObject<object>>, IEnumerable<IFileObject<object>>
    {
        private string path;

        public Directory(Folder obj, IFileObject<object> parent) : base(obj, parent)
        {
            var d = Parent as Directory;
            if (d == null)
                Index = -1;
            else
                Index = d.Object.folders.BinarySearch(Object);
            path = Parent.Path + '/' + Name;

            Cast = (FileObject<object>)(IFileObject<object>)this;
        }

        public sealed override string Name
        {
            get
            {
                return Object.name;
            }

            set
            {
                LogOperation(FileOpLogType.MoveFolder, value, Path, Parent.Path + '/' + Name);
                Object.name = value;
                path = Parent.Path + '/' + Name;
            }
        }

        public override string Path
        {
            get
            {
                return path;
            }

            set
            {
                var d = Root.SeacrhForDirectory(value.Remove(value.LastIndexOf('/')));
                if (d != null)
                    Parent = d.Cast;
                var name = value.Substring(value.LastIndexOf('/') + 1);
                name = name.Length > 0 ? name : Name;
                LogOperation(FileOpLogType.MoveFolder, name, Path, Parent.Path + '/' + Name);
                Object.name = name;
                path = Parent.Path + '/' + Name;
            }
        }

        public sealed override int Index
        {
            get; internal set;
        }

        public sealed override Filesystem Root => Parent.Root;

        public T CastParent<T>() where T : class
        {
            return Parent as T;
        }

        public File FindFile(string name)
        {
            var v = Object.searchForFile(name);
            if (v != null)
                return new File(v, this);
            return null;
        }

        public Directory FindDirectory(string name)
        {
            var v = Object.searchForFolder(name);
            if (v != null)
                return new Directory(v, Cast);
            return null;
        }

        public Directory SeacrhForDirectory(string path)
        {
            var res = this;
            foreach (var p in path.Split('/').Skip(1))
                if ((res = res.FindDirectory(p)) == null)
                    break;
            return res;
        }

        public File SearchForDirectory(string path)
        {
            var pList = path.Split('/').Skip(1);
            string p = null;
            File res = null;
            var dir = this;
            for (int i = 0; i < pList.Count(); p = pList.ElementAt(i++))
            {
                if (i == pList.Count() - 1)
                    res = dir.FindFile(p);
                if ((dir = dir.FindDirectory(p)) == null)
                    break;
            }
            return res;
        }

        public File GetFile(int index)
        {
            return new File(Object.files[index], this);
        }

        public Directory GetDirectory(int index)
        {
            return new Directory(Object.folders[index], Cast);
        }

        public File CreateFile(string name, string data = null)
        {
            if (data == null)
                data = "";
            var r = new File(new FileEntry(name, data), this);
            r.LogOperation(FileOpLogType.CreateFile, data, Path);
            Object.files.Add(r.Object);
            return r;
        }

        public File CreateFile(string name, Executable.IInterface exeInterface)
        {
            return CreateFile(name, Executable.Handler.GetStandardFileDataBy(exeInterface));
        }

        public File CreateExecutableFile(string name, string exeId)
        {
            return CreateFile(name,
                              ExeInfoManager.GetExecutableInfo(exeId).Data
                              ?? Executable.Handler.GetStandardFileDataBy(exeId, true));
        }

        public File CreateExecutableFile(string name, int vanillaIndex)
        {
            return CreateFile(name, ExeInfoManager.GetExecutableInfo(vanillaIndex).Data);
        }

        public Directory CreateDirectory(string name)
        {
            var r = new Directory(new Folder(name), Cast);
            r.LogOperation(FileOpLogType.CreateFolder, Path);
            Object.folders.Add(r.Object);
            return r;
        }

        public bool RemoveFile(string name)
        {
            return RemoveFile(FindFile(name));
        }

        public bool RemoveFile(File f)
        {
            if (f == null)
                return false;
            f.LogOperation(FileOpLogType.DeleteFile, Path);
            return Object.files.Remove(f.Object);
        }

        public bool RemoveDirectory(string name)
        {
            return RemoveDirectory(FindDirectory(name));
        }

        public bool RemoveDirectory(Directory d)
        {
            if (d == null)
                return false;
            d.LogOperation(FileOpLogType.DeleteFolder, Path);
            return Object.folders.Remove(d.Object);
        }

        public File MoveFile(File f, Directory newDir)
        {
            f.LogOperation(FileOpLogType.MoveFile, f.Name, Path, newDir.Path);
            Object.files.RemoveAt(f.Index);
            f.Parent = newDir;
            f.Index = newDir.Object.files.Count;
            newDir.Object.files.Add(f.Object);
            f.Name = f.Name;
            return f;
        }

        public Directory MoveDirectory(Directory d, Directory newDir)
        {
            d.LogOperation(FileOpLogType.MoveFolder, d.Name, Path, newDir.Path);
            Object.folders.RemoveAt(d.Index);
            d.Parent = newDir.Cast;
            d.Index = newDir.Object.folders.Count;
            newDir.Object.folders.Add(d.Object);
            d.Name = d.Name;
            return d;
        }

        public Directory MoveTo(Directory to)
        {
            return CastParent<Directory>()?.MoveDirectory(this, to);
        }

        public bool Contains(File f)
        {
            return Object.files.Contains(f?.Object);
        }

        public bool Contains(Directory d)
        {
            return Object.folders.Contains(d?.Object);
        }

        public bool ContainsFile(string name = null, string data = null)
        {
            if (name == null && data == null)
                return Object.files.Count < 1;
            if (name == null)
                return Object.containsFileWithData(data);
            else if (data == null)
                return Object.containsFile(name);
            else
                return Object.files.Exists(f => f.name == name && f.data == data);
        }

        public bool ContainsDirectory(string name)
        {
            if (name == null)
                return Object.folders.Count < 1;
            return Object.folders.Exists(f => f.name == name);
        }

        public FileObject<object> Cast
        {
            get; private set;
        }

        public IFileObject<object> this[string name] => FindFile(name)?.Cast ?? FindDirectory(name)?.Cast;
        public List<File> Files => Object.files.Select(f => new File(f, this)).ToList();
        public List<Directory> Directories => Object.folders.Select(f => new Directory(f, Cast)).ToList();
        public int FileCount => Object.files.Count;
        public int DirectoryCount => Object.folders.Count;

        public IEnumerator<IFileObject<object>> GetEnumerator()
        {
            foreach (var f in Object.files)
            {
                yield return new File(f, this).Cast;
            }

            foreach (var f in Object.folders)
            {
                yield return new Directory(f, Cast).Cast;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
