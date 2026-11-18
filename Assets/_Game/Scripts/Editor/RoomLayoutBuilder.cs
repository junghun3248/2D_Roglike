using Roglike.Core;
using Roglike.Data;
using Roglike.Run;
using UnityEditor;
using UnityEngine;

namespace Roglike.EditorTools
{
    public static class RoomLayoutBuilder
    {
        public static Room[] Build(AreaConfig area, Sprite gateSprite, Sprite gateClosedArt = null, Sprite gateOpenArt = null)
        {
            var root = new GameObject("Rooms").transform;
            var rooms = new Room[area.rooms.Length];
            float minX = -20f;
            for (int i = 0; i < rooms.Length; i++)
            {
                RoomEntry entry = area.rooms[i];
                float width = entry.type == RoomType.Boss ? 44f : 40f;
                float maxX = minX + width;
                var go = new GameObject($"Room{i}_{entry.type}");
                go.transform.SetParent(root, false);
                var room = go.AddComponent<Room>();
                rooms[i] = room;
                var trigger = go.AddComponent<BoxCollider2D>();
                trigger.isTrigger = true;
                trigger.offset = new Vector2(minX + 2.5f, 9f);
                trigger.size = new Vector2(2f, 20f);
                var left = Gate(go.transform, "LeftDoor", minX, gateSprite, gateClosedArt, out Transform leftVisual);
                var right = Gate(go.transform, "RightDoor", maxX, gateSprite, gateClosedArt, out Transform rightVisual);
                left.enabled = false;
                Transform playerEntry = Point(go.transform, "PlayerEntry", minX + 3f, 1.2f);
                Transform sign = new GameObject("ExitArrow").transform;
                sign.SetParent(go.transform, false);
                // 그림 문이 있으면 열린 문 위에, 없으면 문 앞에 화살표를 띄운다.
                sign.position = gateClosedArt != null ? new Vector3(maxX, 4.8f, 0f) : new Vector3(maxX - 2f, 3f, 0f);
                var text = sign.gameObject.AddComponent<TextMesh>();
                text.text = "→";
                text.fontSize = 64;
                text.characterSize = 0.12f;
                text.anchor = TextAnchor.MiddleCenter;
                text.color = new Color(1f, 0.85f, 0.3f);
                sign.GetComponent<MeshRenderer>().sortingOrder = 30;
                sign.gameObject.SetActive(false);

                Solid("Ground", go.transform, (minX + maxX) * 0.5f, -1f, width, 2f, false);
                Vector2[] platforms = Layout(entry);
                for (int j = 0; j < platforms.Length; j++)
                    Solid("Platform" + j, go.transform, minX + platforms[j].x, platforms[j].y, 6f, 0.5f, true);
                var points = new Transform[4];
                for (int j = 0; j < points.Length; j++)
                    points[j] = Point(go.transform, "Spawn" + j, minX + 10f + j * 7f, 1.5f);
                var serialized = new SerializedObject(room);
                serialized.FindProperty("_minX").floatValue = minX;
                serialized.FindProperty("_maxX").floatValue = maxX;
                serialized.FindProperty("_entryTrigger").objectReferenceValue = trigger;
                serialized.FindProperty("_leftDoor").objectReferenceValue = left;
                serialized.FindProperty("_rightDoor").objectReferenceValue = right;
                serialized.FindProperty("_leftVisual").objectReferenceValue = leftVisual;
                serialized.FindProperty("_rightVisual").objectReferenceValue = rightVisual;
                serialized.FindProperty("_exitSign").objectReferenceValue = sign;
                serialized.FindProperty("_gateClosed").objectReferenceValue = gateClosedArt;
                serialized.FindProperty("_gateOpen").objectReferenceValue = gateOpenArt;
                serialized.FindProperty("_playerEntry").objectReferenceValue = playerEntry;
                var spawns = serialized.FindProperty("_spawnPoints");
                spawns.arraySize = points.Length;
                for (int j = 0; j < points.Length; j++) spawns.GetArrayElementAtIndex(j).objectReferenceValue = points[j];
                serialized.ApplyModifiedPropertiesWithoutUndo();
                minX = maxX;
            }
            return rooms;
        }

        private static Vector2[] Layout(RoomEntry room)
        {
            if (room.type == RoomType.Boss)
                return new[] { new Vector2(8f, 3f), new Vector2(36f, 3f) };
            switch (room.layout)
            {
                case 1: return new[] { new Vector2(8f, 3f), new Vector2(19f, 5.5f), new Vector2(30f, 3.5f) };
                case 2: return new[] { new Vector2(8f, 5.5f), new Vector2(15f, 3f), new Vector2(24f, 6f), new Vector2(32f, 3f) };
                case 3: return new[] { new Vector2(12f, 2.5f), new Vector2(28f, 2.5f) };
                case 4: return new[] { new Vector2(8f, 3f), new Vector2(16f, 6f), new Vector2(25f, 3f), new Vector2(32f, 6f) };
                default: return new[] { new Vector2(13f, 3f), new Vector2(27f, 4f) };
            }
        }

        private static Transform Point(Transform parent, string name, float x, float y)
        {
            var point = new GameObject(name).transform;
            point.SetParent(parent, false);
            point.position = new Vector3(x, y, 0f);
            return point;
        }

        private static BoxCollider2D Gate(Transform parent, string name, float x, Sprite sprite, Sprite closedArt, out Transform visual)
        {
            Transform gate = Point(parent, name, x, 10f);
            gate.gameObject.layer = GameLayers.Ground;
            var collider = gate.gameObject.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(0.8f, 22f);
            if (closedArt != null)
            {
                // 문 그림(가운데 피벗, 키 4단위)을 땅 위에 세운다. 콜라이더는 그대로.
                visual = Point(gate, "GateVisual", x, closedArt.bounds.size.y * 0.5f);
                var art = visual.gameObject.AddComponent<SpriteRenderer>();
                art.sprite = closedArt;
                art.sortingOrder = 11;
                return collider;
            }

            visual = Point(gate, "GateVisual", x, 4f);
            visual.localScale = new Vector3(0.8f, 8f, 1f);
            var renderer = visual.gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = new Color(0.38f, 0.24f, 0.12f);
            renderer.sortingOrder = 10;
            return collider;
        }

        private static void Solid(string name, Transform parent, float x, float y, float width, float height, bool oneWay)
        {
            PrototypeSceneBuilder.CreateSolid(name, parent, new Vector2(x, y), new Vector2(width, height),
                new Color(0.5f, 0.35f, 0.2f), new Color(0.4f, 0.8f, 0.3f), oneWay);
        }
    }
}
