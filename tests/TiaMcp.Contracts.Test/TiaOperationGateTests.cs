using System;
using System.Threading;
using System.Threading.Tasks;
using TiaMcpServer.Runtime;

namespace TiaMcp.Contracts.Test
{
    [TestClass]
    public sealed class TiaOperationGateTests
    {
        [TestMethod]
        public async Task RunAsync_SerialisesConcurrentOperations()
        {
            using var gate = new TiaOperationGate();
            var firstEntered = NewSignal();
            var releaseFirst = NewSignal();
            var secondEntered = NewSignal();

            var first = gate.RunAsync(
                async () =>
                {
                    firstEntered.TrySetResult(true);
                    await releaseFirst.Task;
                    return 1;
                },
                CancellationToken.None);

            await firstEntered.Task;
            var second = gate.RunAsync(
                () =>
                {
                    secondEntered.TrySetResult(true);
                    return Task.FromResult(2);
                },
                CancellationToken.None);

            var earlyCompletion = await Task.WhenAny(
                secondEntered.Task,
                Task.Delay(TimeSpan.FromMilliseconds(100)));
            Assert.AreNotSame(
                secondEntered.Task,
                earlyCompletion,
                "The second operation entered while the first still owned the gate.");

            releaseFirst.TrySetResult(true);
            Assert.AreEqual(1, await first);
            Assert.AreEqual(2, await second);
            Assert.IsTrue(secondEntered.Task.IsCompleted);
        }

        [TestMethod]
        public async Task RunAsync_ReleasesGateAfterOperationFailure()
        {
            using var gate = new TiaOperationGate();

            await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                () => gate.RunAsync<int>(
                    () => throw new InvalidOperationException("expected"),
                    CancellationToken.None));

            var result = await gate.RunAsync(
                () => Task.FromResult(42),
                CancellationToken.None);

            Assert.AreEqual(42, result);
        }

        [TestMethod]
        public async Task RunAsync_CancelledWaiterDoesNotReleaseAnotherOperation()
        {
            using var gate = new TiaOperationGate();
            var firstEntered = NewSignal();
            var releaseFirst = NewSignal();
            var thirdEntered = NewSignal();

            var first = gate.RunAsync(
                async () =>
                {
                    firstEntered.TrySetResult(true);
                    await releaseFirst.Task;
                    return 1;
                },
                CancellationToken.None);
            await firstEntered.Task;

            using var cancellation = new CancellationTokenSource();
            var cancelled = gate.RunAsync(
                () => Task.FromResult(2),
                cancellation.Token);
            cancellation.Cancel();
            await Assert.ThrowsExceptionAsync<OperationCanceledException>(
                async () => await cancelled);

            var third = gate.RunAsync(
                () =>
                {
                    thirdEntered.TrySetResult(true);
                    return Task.FromResult(3);
                },
                CancellationToken.None);
            var earlyCompletion = await Task.WhenAny(
                thirdEntered.Task,
                Task.Delay(TimeSpan.FromMilliseconds(100)));
            Assert.AreNotSame(
                thirdEntered.Task,
                earlyCompletion,
                "A cancelled waiter released a gate owned by another operation.");

            releaseFirst.TrySetResult(true);
            Assert.AreEqual(1, await first);
            Assert.AreEqual(3, await third);
        }

        private static TaskCompletionSource<bool> NewSignal()
        {
            return new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }
}
