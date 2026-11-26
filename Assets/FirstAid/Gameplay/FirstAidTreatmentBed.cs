using System;
using UnityEngine;

namespace FirstAid
{
    internal sealed class FirstAidTreatmentBed
    {
        private readonly Transform bed;
        private readonly Vector3 home;

        public static FirstAidTreatmentBed Create(Transform patientCases)
        {
            var room = GameObject.Find("First Aid Room");
            if (room == null)
                return null;

            var anchor = room.transform.Find("FirstAidRoom/NPC_Bed_Anchor");
            if (anchor == null)
            {
                foreach (var part in room.GetComponentsInChildren<Transform>(true))
                {
                    if (part.name == "NPC_Bed_Anchor")
                    {
                        anchor = part;
                        break;
                    }
                }
            }
            if (anchor == null)
                return null;

            var bed = new GameObject("Treatment Bed Position").transform;
            bed.position = anchor.position;
            var roomParts = new Transform[anchor.parent.childCount];
            for (int index = 0; index < roomParts.Length; index++)
                roomParts[index] = anchor.parent.GetChild(index);

            foreach (var part in roomParts)
            {
                string name = part.name;
                if (name.IndexOf("bed", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("mattress", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.StartsWith("Treatment ", StringComparison.Ordinal) ||
                    name.IndexOf("pillow", StringComparison.OrdinalIgnoreCase) >= 0)
                    part.SetParent(bed, true);
            }

            patientCases.SetParent(bed, true);
            return new FirstAidTreatmentBed(bed);
        }

        private FirstAidTreatmentBed(Transform bed)
        {
            this.bed = bed;
            home = bed.position;
        }

        public void Present(Transform wound)
        {
            bed.SetPositionAndRotation(home, Quaternion.identity);
            if (wound.position.z > home.z)
                bed.rotation = Quaternion.Euler(0f, 180f, 0f);

            float x = wound.position.x;
            float slide = Mathf.Clamp(x, -0.29f, 0.29f) - x;
            bed.position = home + new Vector3(slide, 0f, -0.18f);
            Physics.SyncTransforms();
        }
    }
}
