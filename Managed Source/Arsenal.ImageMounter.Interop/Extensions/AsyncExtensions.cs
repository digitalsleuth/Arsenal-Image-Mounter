﻿using Arsenal.ImageMounter.IO;
using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace Arsenal.ImageMounter.Extensions;

public static class AsyncExtensions
{
    public static readonly Task<int> ZeroCompletedTask = Task.FromResult(0);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SynchronizationContext GetSynchronizationContext(this ISynchronizeInvoke owner) =>
        owner.InvokeRequired ?
        owner.Invoke(new Func<SynchronizationContext>(() => SynchronizationContext.Current), null) as SynchronizationContext :
        SynchronizationContext.Current;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static WaitHandleAwaiter GetAwaiterWithTimeout(this WaitHandle handle, TimeSpan timeout) =>
        new(handle, timeout);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static WaitHandleAwaiter GetAwaiter(this WaitHandle handle) =>
        new(handle, Timeout.InfiniteTimeSpan);

    public static async Task<int> RunProcessAsync(string exe, string args)
    {
        using var ps = new Process
        {
            EnableRaisingEvents = true,
            StartInfo = new ProcessStartInfo
            {
                UseShellExecute = false,
                FileName = exe,
                Arguments = args
            }
        };

        ps.Start();

        return await ps;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ProcessAwaiter GetAwaiter(this Process process) =>
        new(process);

    [SupportedOSPlatform(NativeConstants.SUPPORTED_WINDOWS_PLATFORM)]
    public static WaitHandle CreateWaitHandle(this Process process, bool inheritable) =>
        NativeWaitHandle.DuplicateExisting(process.Handle, inheritable);

    [SupportedOSPlatform(NativeConstants.SUPPORTED_WINDOWS_PLATFORM)]
    private sealed class NativeWaitHandle : WaitHandle
    {
        [DllImport("kernel32")]
        private static extern bool DuplicateHandle(IntPtr hSourceProcessHandle, IntPtr hSourceHandle, IntPtr hTargetProcessHandle, out SafeWaitHandle lpTargetHandle, uint dwDesiredAccess, bool bInheritHandle, uint dwOptions);

        [DllImport("kernel32")]
        private static extern IntPtr GetCurrentProcess();

        public static NativeWaitHandle DuplicateExisting(IntPtr handle, bool inheritable)
        {
            if (!DuplicateHandle(GetCurrentProcess(), handle, GetCurrentProcess(), out var new_handle, 0, inheritable, 0x2))
            {
                throw new Win32Exception();
            }

            return new(new_handle);
        }

        public NativeWaitHandle(SafeWaitHandle handle)
        {
            SafeWaitHandle = handle;
        }
    }
}

public readonly struct ProcessAwaiter : INotifyCompletion
{
    public Process Process { get; }

    public ProcessAwaiter(Process process)
    {
        try
        {
            if (process is null || process.Handle == IntPtr.Zero)
            {
                Process = null;
                return;
            }

            if (!process.EnableRaisingEvents)
            {
                throw new NotSupportedException("Events not available for this Process object.");
            }
        }
        catch (Exception ex)
        {
            throw new NotSupportedException("ProcessAwaiter requires a local, running Process object with EnableRaisingEvents property set to true when Process object was created.", ex);
        }

        Process = process;
    }

    public ProcessAwaiter GetAwaiter() => this;

    public bool IsCompleted => Process.HasExited;

    public int GetResult() => Process.ExitCode;

    public void OnCompleted(Action continuation)
    {
        var completion_counter = 0;

        Process.Exited += (sender, e) =>
        {
            if (Interlocked.Exchange(ref completion_counter, 1) == 0)
            {
                continuation();
            }
        };

        if (Process.HasExited && Interlocked.Exchange(ref completion_counter, 1) == 0)
        {
            continuation();
        }
    }
}

public sealed class WaitHandleAwaiter : INotifyCompletion
{
    private readonly WaitHandle handle;
    private readonly TimeSpan timeout;
    private bool result;

    public WaitHandleAwaiter(WaitHandle handle, TimeSpan timeout)
    {
        this.handle = handle;
        this.timeout = timeout;
    }

    public WaitHandleAwaiter GetAwaiter() => this;

    public bool IsCompleted => handle.WaitOne(0);

    public bool GetResult() => result;

    private sealed class CompletionValues
    {
        public RegisteredWaitHandle callbackHandle;

        public Action continuation;

        public WaitHandleAwaiter awaiter;
    }

    public void OnCompleted(Action continuation)
    {
        var completionValues = new CompletionValues
        {
            continuation = continuation,
            awaiter = this
        };

        completionValues.callbackHandle = ThreadPool.RegisterWaitForSingleObject(
            waitObject: handle,
            callBack: WaitProc,
            state: completionValues,
            timeout: timeout,
            executeOnlyOnce: true);
    }

    private static void WaitProc(object state, bool timedOut)
    {
        var obj = state as CompletionValues;

        obj.awaiter.result = !timedOut;

        while (obj.callbackHandle is null)
        {
            Thread.Sleep(0);
        }

        obj.callbackHandle.Unregister(null);

        obj.continuation();
    }
}
