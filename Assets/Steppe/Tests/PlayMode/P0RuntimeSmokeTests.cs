using System.Collections;
using NUnit.Framework;
using Steppe.Player;
using Steppe.Prototype;
using Steppe.Terrain;
using Steppe.World;
using UnityEngine;
using UnityEngine.TestTools;

namespace Steppe.Tests
{
    public sealed class P0RuntimeSmokeTests
    {
        [UnityTest]
        public IEnumerator PrototypeCreatesFlyingCameraAndStreamsTerrain()
        {
            if (Object.FindAnyObjectByType<SteppePrototypeBootstrap>() == null)
            {
                new GameObject("P0 Test Bootstrap").AddComponent<SteppePrototypeBootstrap>();
            }

            yield return null;
            yield return null;

            var cameraController = Object.FindAnyObjectByType<FlyCameraController>();
            var streamer = Object.FindAnyObjectByType<TerrainChunkStreamer>();

            Assert.That(cameraController, Is.Not.Null);
            Assert.That(streamer, Is.Not.Null);
            Assert.That(streamer.LoadedCount, Is.GreaterThan(0));
            Assert.That(cameraController.GetComponent<Collider>(), Is.Null);
            Assert.That(cameraController.GetComponent<Rigidbody>(), Is.Null);
        }

        [UnityTest]
        public IEnumerator FloatingOriginPreservesAbsoluteFocusPosition()
        {
            var systemObject = new GameObject("Floating Origin Test");
            var focusObject = new GameObject("Focus");
            var worldRoot = new GameObject("World Root");
            var worldChild = new GameObject("World Child");
            worldChild.transform.SetParent(worldRoot.transform, false);
            worldChild.transform.position = new Vector3(50f, 0f, 50f);
            focusObject.transform.position = new Vector3(300f, 10f, -260f);

            var system = systemObject.AddComponent<FloatingOriginSystem>();
            system.Configure(focusObject.transform, worldRoot.transform, 128f, 64f);
            var before = system.LocalToWorld(focusObject.transform.position);

            yield return null;

            var after = system.LocalToWorld(focusObject.transform.position);
            Assert.That(after.X, Is.EqualTo(before.X).Within(0.0001));
            Assert.That(after.Y, Is.EqualTo(before.Y).Within(0.0001));
            Assert.That(after.Z, Is.EqualTo(before.Z).Within(0.0001));
            Assert.That(Mathf.Abs(focusObject.transform.position.x), Is.LessThan(128f));
            Assert.That(Mathf.Abs(focusObject.transform.position.z), Is.LessThan(128f));

            Object.Destroy(systemObject);
            Object.Destroy(focusObject);
            Object.Destroy(worldRoot);
        }
    }
}
