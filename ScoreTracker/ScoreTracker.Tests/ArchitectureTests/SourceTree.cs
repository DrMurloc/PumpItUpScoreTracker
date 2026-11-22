using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ScoreTracker.Tests.ArchitectureTests;

/// <summary>
///     The repo root and the files under it, for ratchets that read source. Every path it hands
///     out is repo-relative with forward slashes.
///     <para>
///         Folders are skipped by their <b>repo-relative</b> path, never the absolute one: a
///         worktree lives under <c>.claude/worktrees/</c>, so a filter on the absolute path that
///         drops <c>.claude</c> would skip the whole tree and leave every scan green over nothing.
///     </para>
/// </summary>
internal static class SourceTree
{
    private static readonly HashSet<string> SkippedFolders =
        new(StringComparer.OrdinalIgnoreCase) { ".git", ".claude", "bin", "obj" };

    /// <summary>The folder that holds <c>docs/</c> and <c>ScoreTracker/ScoreTracker.sln</c>.</summary>
    public static string RepoRoot { get; } = FindRepoRoot();

    /// <summary>The folder that holds <c>ScoreTracker.sln</c> and the projects.</summary>
    public static string SolutionRoot => Path.Combine(RepoRoot, "ScoreTracker");

    /// <summary>
    ///     Every file under the repo root outside <c>.git</c>, <c>.claude/</c>, <c>bin/</c> and
    ///     <c>obj/</c>, as repo-relative paths.
    /// </summary>
    public static IEnumerable<string> Files()
    {
        var pending = new Stack<string>();
        pending.Push(RepoRoot);
        while (pending.Count > 0)
        {
            var directory = pending.Pop();
            // A worktree's .git is a file, not a folder; it is skipped all the same.
            foreach (var file in Directory.EnumerateFiles(directory).Select(Relative).Where(f => !IsSkipped(f)))
                yield return file;
            foreach (var child in Directory.EnumerateDirectories(directory))
                if (!IsSkipped(Relative(child)))
                    pending.Push(child);
        }
    }

    /// <summary>True when any segment of a repo-relative path is a skipped folder.</summary>
    public static bool IsSkipped(string relativePath)
    {
        return relativePath.Split('/', '\\').Any(SkippedFolders.Contains);
    }

    public static string FullPath(string relativePath)
    {
        return Path.Combine(RepoRoot, relativePath);
    }

    public static string ReadAllText(string relativePath)
    {
        return File.ReadAllText(FullPath(relativePath));
    }

    private static string Relative(string path)
    {
        return Path.GetRelativePath(RepoRoot, path).Replace('\\', '/');
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "ScoreTracker", "ScoreTracker.sln")))
            dir = dir.Parent;
        return dir?.FullName
               ?? throw new InvalidOperationException("ScoreTracker/ScoreTracker.sln not found above the test bin directory");
    }
}
