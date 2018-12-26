// <copyright file="ChangeModel.cs" company="AI">
// Copyright (c) AI. All rights reserved.
// </copyright>


using System.IO;
/// <summary>
/// A model that stores the type of change done to a file or directory.
/// </summary>
public class ChangeModel
{
    /// <summary>
    /// Gets the full path to a file or directory.
    /// </summary>
    /// <value>
    /// The full path.
    /// </value>
    public string FullPath { get; internal set; }

    /// <summary>
    /// Gets the type of the change. Can be create, rename, copy, delete.
    /// </summary>
    /// <value>
    /// The type of the change.
    /// </value>
    public WatcherChangeTypes ChangeType { get; internal set; }

    /// <summary>
    /// Gets the old full path. Only used by the a rename
    /// </summary>
    /// <value>
    /// The old full path.
    /// </value>
    public string OldFullPath { get; internal set; }
}
