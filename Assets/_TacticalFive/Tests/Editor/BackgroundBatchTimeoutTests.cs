using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.Text.RegularExpressions;

/// <summary>
/// F8-07: la espera de un lote de fondo devuelve un resultado explícito y no deja
/// arrancar una segunda conexión sobre el mismo archivo.
/// </summary>
public class BackgroundBatchTimeoutTests
{
    [TearDown]
    public void TearDown()
    {
        SetRunningBatch(null);
        DatabaseManager.CancelBackgroundBatches();
    }

    [Test]
    public void WaitForBackgroundBatch_ReturnsTimedOutWithoutClearingTheBatch()
    {
        using var gate = new ManualResetEventSlim(false);
        var blocked = Task.Run(() => gate.Wait(5000));
        SetRunningBatch(blocked);

        LogAssert.Expect(LogType.Error, new Regex("Timeout esperando el lote de fondo"));
        Assert.That(DatabaseManager.WaitForBackgroundBatch(10),
            Is.EqualTo(BackgroundBatchWaitResult.TimedOut));

        gate.Set();
        Assert.That(DatabaseManager.WaitForBackgroundBatch(2000),
            Is.EqualTo(BackgroundBatchWaitResult.Completed));
    }

    [Test]
    public void WaitForBackgroundBatch_ReturnsCompletedForFinishedTask()
    {
        SetRunningBatch(Task.CompletedTask);
        Assert.That(DatabaseManager.WaitForBackgroundBatch(100),
            Is.EqualTo(BackgroundBatchWaitResult.Completed));
    }

    [Test]
    public void WaitForBackgroundBatch_TreatsCancellationAsCompleted()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();
        SetRunningBatch(Task.FromCanceled(cts.Token));

        Assert.That(DatabaseManager.WaitForBackgroundBatch(100),
            Is.EqualTo(BackgroundBatchWaitResult.Completed));
        cts.Dispose();
    }

    [Test]
    public void WaitForBackgroundBatch_ReturnsFaultedAndExposesTheError()
    {
        SetRunningBatch(Task.FromException(new InvalidOperationException("fallo de lote")));
        LogAssert.Expect(LogType.Error, new Regex("El lote de fondo terminó con error"));
        Assert.That(DatabaseManager.WaitForBackgroundBatch(100),
            Is.EqualTo(BackgroundBatchWaitResult.Faulted));
        Assert.That(DatabaseManager.LastBackgroundBatchError, Does.Contain("fallo de lote"));
    }

    static void SetRunningBatch(Task task)
    {
        typeof(DatabaseManager)
            .GetField("_runningBatch", BindingFlags.NonPublic | BindingFlags.Static)
            ?.SetValue(null, task);
    }
}
