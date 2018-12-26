// <copyright file="FileSystemMonitor.cs" company="AI">
// Copyright (c) AI. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

/// <summary>
/// This class watches for changes in a souce directory and automatically syncs files to a .ivdev directroy.
/// </summary>
/// <seealso cref="IDisposable" />
public abstract class FileSystemMonitor : IDisposable
{
    /// <summary>
    /// The action queue. A collection of actions that perform file or directory mutations.
    /// </summary>
    private readonly Dictionary<int, Action> actionQueue;

    /// <summary>
    /// A lock object that prevents the actionQueue from being mutated while actions are running.
    /// </summary>
    private readonly object actionQueueLock = new object();

    /// <summary>
    /// The path
    /// </summary>
    private readonly string path;

    /// <summary>
    /// The file filter
    /// </summary>
    private readonly string fileFilter;

    /// <summary>
    /// The watcher. Responsible for keeping trach of changes in the source directroy and
    /// passing them to the relevant methods.
    /// </summary>
    private FileSystemWatcher watcher;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileSystemMonitor" /> class.
    /// </summary>
    /// <param name="pathToMonitor">The path.</param>
    /// <param name="fileFilter">The file filter.</param>
    public FileSystemMonitor(string pathToMonitor, string fileFilter)
    {
        this.actionQueue = new Dictionary<int, Action>();
        this.path = pathToMonitor;
        this.fileFilter = fileFilter;
    }

    /// <summary>
    /// Thrown when an exception occurs while executing an action.
    /// </summary>
    /// <param name="exception">The exception.</param>
    public delegate void OnException(System.Exception exception);

    /// <summary>
    /// Occurs when [on exception event].
    /// </summary>
    public event OnException OnExceptionEvent;

    /// <summary>
    /// Abstract method that creates an action to perform when a file changes.
    /// </summary>
    /// <param name="changeModel">The change model.</param>
    /// <returns>
    /// An action to perform when a change event occurs
    /// </returns>
    public abstract Action CreateAction(ChangeModel changeModel);

    /// <summary>
    /// Watches the specified path for an file changes.
    /// </summary>
    public void SyncBegin()
    {
        this.watcher = new FileSystemWatcher
        {
            Path = this.path,
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.LastAccess |
            NotifyFilters.FileName |
            NotifyFilters.DirectoryName,
            Filter = this.fileFilter,
            IncludeSubdirectories = true,
        };

        // These events create a model and pass this to HandleChange (below).
        var changeEvent = new FileSystemEventHandler(this.OnFileEvent);

        this.watcher.Changed += changeEvent;
        this.watcher.Created += changeEvent;
        this.watcher.Deleted += changeEvent;
        this.watcher.Renamed += new RenamedEventHandler(this.OnFileEvent);
        this.watcher.EnableRaisingEvents = true;
    }

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    public void Dispose()
    {
        this.watcher.Dispose();
    }

    /// <summary>
    /// Handles the change.
    /// </summary>
    /// <param name="change">The change.</param>
    private void HandleChange(ChangeModel change)
    {
        var changeAction = this?.CreateAction(change);

        // Use a hash code to save unique change actions to the action queue.
        var changeHash = new { change.FullPath, change.OldFullPath, change.ChangeType }.GetHashCode();
        if (!this.actionQueue.ContainsKey(changeHash))
        {
            // Block this thread from making changed to the action queue if the actions are
            // being executed.
            lock (this.actionQueueLock)
            {
                this.actionQueue.Add(changeHash, changeAction);
            }
        }

        try
        {
            this.ExecuteQueuedActions();
        }
        catch (Exception ex)
        {
            this.OnExceptionEvent?.Invoke(ex);
        }
    }

    /// <summary>
    /// Executes the queued actions.
    /// </summary>
    private void ExecuteQueuedActions()
    {
        // Use a timer to delay the execution of action due to the fact that the OnChange event
        // Is called twice in a row when VSCode does a safe.
        Timer timer = null;
        timer = new Timer(
        (o) =>
        {
            // Prevent changes to this.actionQueue from occuring while
            // actions
            lock (this.actionQueueLock)
            {
                foreach (var action in this.actionQueue.Values)
                {
                    action?.Invoke();
                }

                this.actionQueue.Clear();
                timer.Dispose();
            }
        },
        null,
        200,
        Timeout.Infinite);
    }

    /// <summary>
    /// Called by the FileSystemWatcher when any file changes or is renamed.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="e">The <see cref="System.IO.FileSystemEventArgs" /> instance containing the event data.</param>
    private void OnFileEvent(object source, FileSystemEventArgs e)
    {
        var change = new ChangeModel
        {
            FullPath = e.FullPath,
            ChangeType = e.ChangeType
        };

        if (e is RenamedEventArgs renamedEventArgs)
        {
            change.OldFullPath = renamedEventArgs.OldFullPath;
        }

        this.HandleChange(change);
    }
}
