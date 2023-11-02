using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using VRC.SDK3.Data;

namespace DerpyNewbie.VoiceComms.Tests
{
    public class VoiceCommsManagerTests
    {
        private GameObject _tempObject;
        private VoiceCommsManager _instance;

        [SetUp]
        public void SetUp()
        {
            _tempObject = new GameObject();
            _instance = _tempObject.AddComponent<VoiceCommsManager>();

            Assert.That(_instance, Is.Not.Null);

            SetJsonData(_instance, "{\"1\":[1,2,3],\"2\":[2,3,4],\"3\":[5]}");
            _instance._AddRxChannel(1);
            _instance._AddRxChannel(2);
        }

        [TearDown]
        public void TearDown()
        {
            _instance = null;
            Object.Destroy(_tempObject);
        }

        [Test]
        public void TestIsTransmitting()
        {
            CheckFalse(int.MinValue);
            CheckFalse(-100);
            CheckFalse(-1);
            CheckFalse(0);
            CheckTrue(1);
            CheckTrue(2);
            CheckTrue(3);
            CheckFalse(4);
            CheckFalse(100);
            CheckFalse(int.MaxValue);

            return;

            void CheckTrue(int playerId) => Assert.That(_instance._IsTransmitting(playerId), Is.True);
            void CheckFalse(int playerId) => Assert.That(_instance._IsTransmitting(playerId), Is.False);
        }

        [Test]
        public void TestGetTxChannel()
        {
            var empty = new DataList();

            Check(int.MinValue, empty);
            Check(-100, empty);
            Check(-1, empty);
            Check(0, empty);
            Check(1, new DataList { 1D, 2D, 3D });
            Check(2, new DataList { 2D, 3D, 4D });
            Check(3, new DataList { 5D });
            Check(4, empty);
            Check(100, empty);
            Check(int.MaxValue, empty);

            return;

            void Check(int playerId, IEnumerable list) =>
                Assert.That(_instance._GetTxChannels(playerId), Is.EquivalentTo(list));
        }

        [Test]
        public void TestGetActiveTxChannel()
        {
            var empty = new DataList();

            Check(int.MinValue, empty);
            Check(-100, empty);
            Check(0, empty);
            Check(1, new DataList { 1D, 2D });
            Check(2, new DataList { 2D });
            Check(3, empty);
            Check(4, empty);
            Check(100, empty);
            Check(int.MaxValue, empty);

            return;

            void Check(int playerId, IEnumerable list) =>
                Assert.That(_instance._GetActiveTxChannels(playerId), Is.EquivalentTo(list));
        }

        private static void SetJsonData(VoiceCommsManager manager, string jsonData)
        {
            var f = typeof(VoiceCommsManager).GetField("_vcUserDataJson",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.That(f, Is.Not.Null);

            f.SetValue(manager, jsonData);
        }
    }
}