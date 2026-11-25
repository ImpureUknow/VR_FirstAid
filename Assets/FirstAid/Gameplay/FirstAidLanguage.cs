using System;
using System.Collections.Generic;
using System.Globalization;

namespace FirstAid
{
    // Keep English as the stable lookup key; unknown text remains readable.
    internal static class FirstAidLanguage
    {
        private static readonly string[] CaseNames =
        {
            "Abrasion", "Shallow cut", "Hot-water scald"
        };

        private static readonly Dictionary<string, string> Thai = new(StringComparer.Ordinal)
        {
            // Cases and practice sites.
            { "Abrasion", "แผลถลอก" },
            { "Shallow cut", "แผลมีดบาดตื้น" },
            { "Hot-water scald", "แผลน้ำร้อนลวก" },
            { "Hot water scald", "แผลน้ำร้อนลวก" },
            { "01   Abrasion", "01   แผลถลอก" },
            { "02   Shallow cut", "02   แผลมีดบาดตื้น" },
            { "03   Hot-water scald", "03   แผลน้ำร้อนลวก" },
            { "Left knee", "เข่าซ้าย" },
            { "Right elbow", "ข้อศอกขวา" },
            { "Right forearm", "ท่อนแขนขวา" },
            { "Left shin", "หน้าแข้งซ้าย" },
            { "Left forearm", "ท่อนแขนซ้าย" },
            { "Back of right hand", "หลังมือขวา" },
            { "LEFT KNEE", "เข่าซ้าย" },
            { "RIGHT ELBOW", "ข้อศอกขวา" },
            { "RIGHT FOREARM", "ท่อนแขนขวา" },
            { "LEFT SHIN", "หน้าแข้งซ้าย" },
            { "LEFT FOREARM", "ท่อนแขนซ้าย" },
            { "BACK OF RIGHT HAND", "หลังมือขวา" },

            // Checklist steps.
            { "Inspect the wound", "ตรวจแผล" },
            { "Rinse the graze", "ล้างแผลถลอก" },
            { "Cover the graze", "ปิดแผลถลอก" },
            { "Control the bleeding", "ห้ามเลือด" },
            { "Stop the bleeding", "ห้ามเลือด" },
            { "Rinse the cut", "ล้างแผลมีดบาด" },
            { "Cover the cut", "ปิดแผลมีดบาด" },
            { "Cool with running water", "ลดความร้อนด้วยน้ำไหล" },
            { "Cool the scald", "ลดความร้อนที่แผลลวก" },
            { "Cover the scald", "คลุมแผลน้ำร้อนลวก" },

            // Instructions and reasons.
            { "Rinse the marked spot with clean water or saline.", "ล้างจุดที่ทำเครื่องหมายด้วยน้ำสะอาดหรือน้ำเกลือ" },
            { "Rinsing removes grit that can stay under a dressing.", "การล้างช่วยขจัดสิ่งสกปรกก่อนปิดแผล" },
            { "Place a sterile dressing over the wound.", "วางวัสดุปิดแผลปลอดเชื้อบนแผล" },
            { "The dressing protects the cleaned area.", "วัสดุปิดแผลช่วยปกป้องบริเวณที่ล้างแล้ว" },
            { "Hold clean gauze against the marked cut.", "กดผ้าก๊อซสะอาดค้างไว้ที่แผลมีดบาด" },
            { "Pressure helps stop bleeding before you clean the cut.", "การกดช่วยห้ามเลือดก่อนล้างแผล" },
            { "Use clean water or saline once the bleeding stops.", "เมื่อเลือดหยุด ให้ล้างด้วยน้ำสะอาดหรือน้ำเกลือ" },
            { "Cleaning before covering helps remove dirt.", "ล้างสิ่งสกปรกออกก่อนปิดแผล" },
            { "Place a sterile dressing over the cleaned cut.", "วางวัสดุปิดแผลปลอดเชื้อบนแผลที่ล้างแล้ว" },
            { "A dressing protects the cleaned wound.", "วัสดุปิดแผลช่วยปกป้องแผลที่ล้างแล้ว" },
            { "Select the sink tap to simulate 20 min of cooling.", "เลือกก๊อกน้ำเพื่อจำลองการลดความร้อน 20 นาที" },
            { "Running water carries heat away from the skin.", "น้ำไหลช่วยระบายความร้อนออกจากผิวหนัง" },
            { "Lay a loose sheet of cling film over the scald.", "วางแผ่นฟิล์มคลุมแผลลวกหลวม ๆ" },
            { "A loose cover protects the skin and leaves space for swelling.", "การคลุมหลวม ๆ ช่วยปกป้องผิวและเผื่อพื้นที่เมื่อบวม" },
            { "Do not wrap cling film around the limb.", "อย่าพันฟิล์มรอบแขนหรือขา" },
            { "Inspect the injury before choosing a treatment.", "ตรวจแผลก่อนเลือกวิธีดูแล" },
            { "Inspect the marked injury to begin.", "ตรวจจุดแผลที่ทำเครื่องหมายเพื่อเริ่มฝึก" },
            { "Simulating cooling under running water.", "กำลังจำลองการลดความร้อนด้วยน้ำไหล" },
            { "Wound covered. Try another site or choose a new case.", "ปิดแผลแล้ว ลองตำแหน่งอื่นหรือเลือกเคสใหม่" },
            { "Follow the steps at your own pace.", "ฝึกตามลำดับได้ตามสะดวก" },
            { "Look at the highlighted area.", "สังเกตบริเวณที่ทำเครื่องหมาย" },
            { "Step complete. Return the tool and continue.", "ขั้นตอนนี้เสร็จแล้ว คืนอุปกรณ์และทำต่อ" },

            // Wound observations.
            { "The skin looks scraped and may have dirt on the surface.", "ผิวหนังถลอก และอาจมีสิ่งสกปรกบนผิวแผล" },
            { "A narrow break in the skin is bleeding.", "ผิวหนังมีรอยบาดแคบและมีเลือดออก" },
            { "The skin is red with small blisters after contact with hot water.", "ผิวแดงและมีตุ่มพองเล็ก ๆ หลังสัมผัสน้ำร้อน" },

            { "Tap on.", "เปิดน้ำแล้ว" },
            { "Tap off.", "ปิดน้ำแล้ว" },
            { "Hands washed. Put on gloves.", "ล้างมือครบแล้ว ใส่ถุงมือต่อ" },
            { "Left hand washed.", "ล้างมือซ้ายแล้ว" },
            { "Right hand washed.", "ล้างมือขวาแล้ว" },
            { "Wash this hand first.", "ล้างมือข้างนี้ก่อน" },
            { "Both gloves on.", "ใส่ถุงมือครบทั้งสองข้างแล้ว" },
            { "Left glove on.", "ใส่ถุงมือซ้ายแล้ว" },
            { "Right glove on.", "ใส่ถุงมือขวาแล้ว" },
            { "Water bottle full.", "เติมขวดน้ำเต็มแล้ว" },
            { "Turn tap off", "ปิดก๊อกน้ำ" },
            { "Turn tap on", "เปิดก๊อกน้ำ" },
            { "Glove box", "กล่องถุงมือ" },
            { "Water empty. Refill at the tap.", "น้ำหมด เติมขวดที่ก๊อกน้ำ" },
            { "Saline empty. Pick up a fresh bottle.", "น้ำเกลือหมด หยิบขวดใหม่ที่โต๊ะ" },
            { "Open the tap and wash both hands.", "เปิดก๊อกแล้วล้างมือทั้งสองข้าง" },
            { "Use the glove box with each hand.", "หยิบถุงมือจากกล่องทีละข้าง" },

            // Equipment labels and pickup feedback.
            { "Clean water", "น้ำสะอาด" },
            { "Water", "น้ำสะอาด" },
            { "Gauze", "ผ้าก๊อซ" },
            { "Dressing", "วัสดุปิดแผล" },
            { "Cling film", "ฟิล์มคลุมแผล" },
            { "Film", "ฟิล์มคลุมแผล" },
            { "Saline", "น้ำเกลือ" },
            { "Tap", "ก๊อกน้ำ" },
            { "Sink tap", "ก๊อกน้ำที่อ่าง" },
            { "WATER", "น้ำสะอาด" },
            { "GAUZE", "ผ้าก๊อซ" },
            { "DRESSING", "วัสดุปิดแผล" },
            { "FILM", "ฟิล์ม" },
            { "SALINE", "น้ำเกลือ" },
            { "TAP", "ก๊อกน้ำ" },
            { "Bring clean water to the wound marker.", "นำน้ำสะอาดไปที่เครื่องหมายแผล" },
            { "Bring gauze to the wound marker.", "นำผ้าก๊อซไปที่เครื่องหมายแผล" },
            { "Bring dressing to the wound marker.", "นำวัสดุปิดแผลไปที่เครื่องหมายแผล" },
            { "Bring cling film to the wound marker.", "นำฟิล์มไปที่เครื่องหมายแผล" },
            { "Bring saline to the wound marker.", "นำน้ำเกลือไปที่เครื่องหมายแผล" },
            { "Bring {0} to the wound marker.", "นำ{0}ไปที่เครื่องหมายแผล" },

            // Buttons, headings and status. Format these keys before inserting values.
            { "FIRST AID VR", "FIRST AID VR" },
            { "MMI320   /   TRAINING ROOM", "MMI320   /   ห้องฝึกปฐมพยาบาล" },
            { "DESKTOP PRACTICE", "ฝึกด้วยเมาส์" },
            { "VR PRACTICE", "ฝึกด้วย VR" },
            { "SELECT A CASE", "เลือกเคสฝึก" },
            { "INSPECT THE INJURY", "ตรวจแผลก่อนเริ่ม" },
            { "SITE COMPLETE", "ฝึกตำแหน่งนี้สำเร็จ" },
            { "CASE COMPLETE", "ฝึกเคสนี้สำเร็จ" },
            { "SITES COMPLETE", "ตำแหน่งที่ฝึกสำเร็จ" },
            { "DONE", "ครบแล้ว" },
            { "Inspect wound", "ตรวจแผล" },
            { "Inspect", "ตรวจแผล" },
            { "Switch site", "เปลี่ยนตำแหน่ง" },
            { "Other site", "อีกตำแหน่ง" },
            { "Replay case", "ฝึกใหม่" },
            { "Replay", "ฝึกใหม่" },
            { "Next case", "เคสถัดไป" },
            { "Open tap", "เปิดก๊อกน้ำ" },
            { "Close tap", "ปิดก๊อกน้ำ" },
            { "Start cooling", "เริ่มลดความร้อน" },
            { "Language", "ภาษา" },
            { "Thai", "ไทย" },
            { "English", "อังกฤษ" },
            { "Site {0} / 2", "ตำแหน่ง {0} / 2" },
            { "Site 1 / 2", "ตำแหน่ง 1 / 2" },
            { "Site 2 / 2", "ตำแหน่ง 2 / 2" },
            { "STEP {0} OF {1}", "ขั้นตอน {0} / {1}" },
            { "Hints used  {0}", "คำแนะนำที่ได้รับ  {0}" },
            { "{0} / {1} SITES COMPLETE", "ฝึกสำเร็จ {0} / {1} ตำแหน่ง" },
            { "0 / 6 SITES COMPLETE", "ฝึกสำเร็จ 0 / 6 ตำแหน่ง" },
            { "Practice reference  ·  NHS first aid", "อ้างอิงการฝึก  ·  NHS" },
            { "20 min of cooling (simulated)", "จำลองการลดความร้อน 20 นาที" },

            // Local wound tooltip keys, shared by mouse and XR interactions.
            { "Hold above wound", "ถือไว้เหนือแผล" },
            { "Hold above the wound.", "ถือไว้เหนือแผล" },
            { "Hold the bottle above the wound.", "ถือขวดไว้เหนือแผล" },
            { "Move the bottle above the wound.", "เลื่อนขวดมาเหนือแผล" },
            { "Tilt bottle", "เอียงขวด" },
            { "Tilt the bottle.", "เอียงขวด" },
            { "Tilt the bottle to pour.", "เอียงขวดเพื่อเทน้ำ" },
            { "Keep the bottle tilted.", "เอียงขวดค้างไว้" },
            { "Wrong order", "ยังไม่ถึงขั้นตอนนี้" },
            { "Wrong order.", "ยังไม่ถึงขั้นตอนนี้" },
            { "Complete the current step first.", "ทำขั้นตอนปัจจุบันให้เสร็จก่อน" },
            { "Pour too far", "เทห่างจากแผลเกินไป" },
            { "Pouring too far from the wound.", "เทห่างจากแผลเกินไป" },
            { "Move closer to the wound.", "เลื่อนเข้าใกล้แผลอีกนิด" },
            { "Aim the stream at the wound.", "ให้น้ำไหลลงตรงแผล" },
            { "Pouring", "กำลังเทน้ำ" },
            { "Rinsing", "กำลังล้างแผล" },
            { "Cooling", "กำลังลดความร้อน" },
            { "Hold steady", "ถือค้างไว้" },
            { "Keep holding.", "ค้างไว้ต่อ" },
            { "Ready", "พร้อม" },
            { "Success", "สำเร็จ" },
            { "Step complete", "ขั้นตอนนี้เสร็จแล้ว" },
            { "Rinse complete.", "ล้างแผลแล้ว" },
            { "Bleeding controlled.", "กดห้ามเลือดแล้ว" },
            { "Wound covered.", "ปิดแผลแล้ว" },
            { "Cooling complete.", "จำลองการลดความร้อนเสร็จแล้ว" },
            { "Return the tool.", "คืนอุปกรณ์" },
            { "Inspect first", "ตรวจแผลก่อน" },

            // Input help. Keep key names unchanged so they match the actual bindings.
            { "Click a tool. Hold it over the wound.\nRight-click to return.  I Inspect  B Switch site  R Replay",
                "คลิกอุปกรณ์ แล้วค้างไว้เหนือแผล\nคลิกขวาคืน  I ตรวจแผล  B เปลี่ยนจุด  R ฝึกใหม่" },
            { "Point and trigger at the menu.\nGrip to use the tap or hold a tool at the wound.",
                "ชี้เมนูแล้วกด Trigger\nกด Grip เพื่อใช้ก๊อกน้ำหรือถืออุปกรณ์ที่แผล" },
            { "Point and trigger to use the menu.", "ชี้เมนูแล้วกด Trigger" },
            { "Grip to pick up a tool.", "กด Grip เพื่อหยิบอุปกรณ์" },
            { "Release grip to return the tool.", "ปล่อย Grip เพื่อคืนอุปกรณ์" },
            { "Click a tool to pick it up.", "คลิกอุปกรณ์เพื่อหยิบ" },
            { "Right-click to return.", "คลิกขวาเพื่อคืน" },
            { "Hold left mouse to tilt the bottle.", "กดเมาส์ซ้ายค้างเพื่อเอียงขวด" },
            { "Release left mouse to stop pouring.", "ปล่อยเมาส์ซ้ายเพื่อหยุดเท" },
            { "Tilt your controller to pour.", "เอียงคอนโทรลเลอร์เพื่อเท" },
            { "1–3 Cases   I Inspect   B Switch site   R Replay",
                "1–3 เลือกเคส   I ตรวจแผล   B เปลี่ยนจุด   R ฝึกใหม่" },
            { "F2  XR controls", "F2  ควบคุมแบบ XR" },
            { "F2  Mouse controls", "F2  ควบคุมด้วยเมาส์" },
            { "\nF2  XR controls", "\nF2  ควบคุมแบบ XR" },
            { "\nF2  Mouse controls", "\nF2  ควบคุมด้วยเมาส์" },
            { "WASD Move   Right mouse Look   G Grip   T Trigger",
                "WASD เคลื่อนที่   เมาส์ขวาหันมอง   G จับ   T เลือก" },
            { "Tab Device mode   [ Left   ] Right",
                "Tab เปลี่ยนโหมด   [ มือซ้าย   ] มือขวา" }
        };

        public static string Get(string english, bool thai)
        {
            if (!thai || string.IsNullOrEmpty(english))
                return english;

            return Thai.TryGetValue(english, out string translated) ? translated : english;
        }

        public static string CaseName(int index, bool thai)
        {
            return index >= 0 && index < CaseNames.Length ? Get(CaseNames[index], thai) : string.Empty;
        }

        // Translate the format key first, then insert numbers or translated tool names.
        public static string Format(string englishFormat, bool thai, params object[] values)
        {
            return string.Format(CultureInfo.InvariantCulture, Get(englishFormat, thai), values);
        }
    }
}
