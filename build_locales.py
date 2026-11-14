import glob
import os
import struct
from pathlib import Path

def compile_po_to_mo(po_path: Path, mo_path: Path):
    messages = {}
    current_msgid = None
    current_msgstr = None
    state = None
    is_fuzzy = False

    def save_entry():
        nonlocal current_msgid, current_msgstr, is_fuzzy
        if current_msgid is not None and current_msgstr is not None:
            if not is_fuzzy and (current_msgid == "" or current_msgstr != ""):
                messages[current_msgid] = current_msgstr
        current_msgid = None
        current_msgstr = None
        is_fuzzy = False

    def unescape(s: str) -> str:
        s = s.strip()
        if s.startswith('"') and s.endswith('"'):
            s = s[1:-1]
        return (
            s.replace(r'\n', '\n')
             .replace(r'\t', '\t')
             .replace(r'\"', '"')
             .replace(r'\\', '\\')
        )

    for raw_line in po_path.read_text(encoding="utf-8", errors="replace").splitlines():
        line = raw_line.strip()
        if not line:
            save_entry()
            state = None
            continue
        if line.startswith("#,") and "fuzzy" in line:
            is_fuzzy = True
            continue
        if line.startswith("#"):
            continue
        if line.startswith("msgid "):
            save_entry()
            current_msgid = unescape(line[6:])
            state = "msgid"
        elif line.startswith("msgstr "):
            current_msgstr = unescape(line[7:])
            state = "msgstr"
        elif line.startswith('"') and state == "msgid" and current_msgid is not None:
            current_msgid += unescape(line)
        elif line.startswith('"') and state == "msgstr" and current_msgstr is not None:
            current_msgstr += unescape(line)

    save_entry()

    keys = sorted(messages.keys())
    offsets = []
    ids = b""
    strs = b""
    for k in keys:
        b_id = k.encode("utf-8")
        b_str = messages[k].encode("utf-8")
        offsets.append((len(ids), len(b_id), len(strs), len(b_str)))
        ids += b_id + b"\x00"
        strs += b_str + b"\x00"

    n = len(keys)
    keystart = 7 * 4 + 16 * n
    valuestart = keystart + len(ids)
    koffsets = []
    voffsets = []
    for o1, l1, o2, l2 in offsets:
        koffsets += [l1, o1 + keystart]
        voffsets += [l2, o2 + valuestart]

    output = struct.pack(
        "Iiiiiii",
        0x950412de,  # Magic
        0,           # Version
        n,           # # of entries
        7 * 4,       # start of key index
        7 * 4 + 8 * n, # start of value index
        0, 0         # size and offset of hash table
    )
    output += struct.pack(f"{2*n}i", *koffsets)
    output += struct.pack(f"{2*n}i", *voffsets)
    output += ids
    output += strs

    mo_path.parent.mkdir(parents=True, exist_ok=True)
    mo_path.write_bytes(output)

def generate_tr_po(repo_po_dir: Path):
    tr_dict = {
        "Boxes.py": "Boxes.py",
        "Create boxes and more with a laser cutter!": "Lazer kesici ile kutular ve mekanik parçalar üretin!",
        "Help": "Yardım",
        "Home Page": "Ana Sayfa",
        "Documentation": "Dokümantasyon",
        "Sources": "Kaynak Kod",
        "Legal": "Yasal",
        "Give Back": "Destek Ol",
        "Preview": "Canlı Önizleme",
        "Search": "Kutu Ara...",
        "Gallery": "Görsel Galeri",
        "Menu": "Kategorik Menü",
        "Generate": "Oluştur (Yeni Sekme)",
        "Download": "Dosyayı İndir",
        "Save to URL": " Bağlantıyı Kaydet",
        "QR Code": "QR Kod",
        "Zoom: ": "Yakınlaştır: ",
        "Reset": "Sıfırla",
        "An error occurred!": "Bir hata oluştu!",
        "Boxes": "Kutular (Boxes)",
        "Boxes with flex": "Esnek Köşeli Kutular (Flex)",
        "Trays and Drawer Inserts": "Tepsiler ve Çekmece İçi Düzenleyiciler",
        "Shelves": "Raflar ve Standlar",
        "WallMounted": "Duvara Monte Aparatlar",
        "Hole patterns": "Delik Şablonları ve Paneller",
        "Parts and Samples": "Dişliler, Parçalar ve Test Şablonları",
        "Misc": "Çeşitli Projeler ve Araçlar",
        "Unstable": "Deneysel Şablonlar",
        "Default Settings": "Temel Malzeme ve Lazer Ayarları",
        "Settings for Finger Joints": "Geçme Diş (Finger Joint) Ayarları",
        "Settings for Stackable Edges": "İstiflenebilir Kenar Ayarları",
        "Settings for Hinges and HingePins": "Menteşe ve Pim Ayarları",
        "Settings for Slide-on Lids": "Sürgülü Kapak Ayarları",
        "Settings for Click-on Lids": "Geçmeli (Klipsli) Kapak Ayarları",
        "Settings for Flex": "Esnek Kesim (Living Hinge) Ayarları",
        "Settings for Chest Hinges": "Sandık Menteşesi Ayarları",
        "Settings for Cabinet Hinges": "Dolap Menteşesi Ayarları",
        "Settings for Dove Tails": "Kırlangıç Kuyruğu (Dovetail) Ayarları",
        "Settings for Grip Edges": "Tutma Kenarı Ayarları",
        "thickness": "kalınlık (mm)",
        "thickness of the material (in mm)": "malzeme kalınlığı (mm cinsinden)",
        "format": "çıktı formatı",
        "format of resulting file": "üretilecek dosyanın formatı (SVG, DXF, LightBurn, GCode, PDF)",
        "tabs": "tutucu tırnak (tabs)",
        "labels": "parça etiketleri",
        "label the parts (where available)": "parçaların üzerine isimlerini yaz",
        "reference": "referans ölçek (mm)",
        "inner_corners": "iç köşe tipi",
        "style for inner corners": "iç köşeler için kesim stili (loop, corner, backarc)",
        "burn": "lazer yakma payı / kerf (mm)",
        "burn correction in mm (bigger values for tighter fit)": "mm cinsinden lazer ışını erime/yakma telafisi (daha sıkı geçme için değeri artırın)",
        "outside": "dış ölçü baz al",
        "treat sizes as outside measurements": "girilen x, y, h ölçülerini iç değil dış ölçü olarak hesapla",
        "bottom_edge": "alt kenar tipi",
        "top_edge": "üst kenar tipi",
        "x": "x (genişlik mm)",
        "inner width in mm (unless outside selected)": "mm cinsinden iç genişlik (dış ölçü seçilmediyse)",
        "y": "y (derinlik mm)",
        "inner depth in mm (unless outside selected)": "mm cinsinden iç derinlik (dış ölçü seçilmediyse)",
        "h": "h (yükseklik mm)",
        "inner height in mm (unless outside selected)": "mm cinsinden iç yükseklik (dış ölçü seçilmediyse)",
        "sx": "sx (X bölmeleri)",
        "sy": "sy (Y bölmeleri)",
        "sh": "sh (Yükseklik katmanları)",
        "spacing": "parça aralığı",
        "debug": "hata ayıklama çizgileri",
        "qr_code": "QR Kod ekle",
        "A simple Box": "Basit geçmeli kutu",
        "Fully closed box": "Tamamen kapalı kutu",
        "Box with various options for different styles and lids": "Farklı kapak ve kenar seçeneklerine sahip çok amaçlı kutu",
        "Box with living hinge and round corners": "Esnek kesimli (living hinge) ve yuvarlatılmış köşeli kutu",
        "Sliding drawer box": "Sürgülü çekmeceli kutu",
        "Box with rails for a sliding lid": "Sürgülü kapak raylı kutu",
        "Type tray - allows only continuous walls": "Bölmeli organizatör tepsisi",
        "Divider tray - rows and dividers": "Ayarlanabilir ayraçlı çekmece/malzeme tepsisi",
        "Test different burn values": "Farklı lazer kerf (yakma/sıkılık) değerlerini test etme şablonu",
        "Gears": "Dişli çark üretici",
        "Planetary Gear with possibly multiple identical stages": "Planet dişli mekanizması üretici",
        "Timing belt pulleys for different profiles": "Triger kayışı kasnağı üretici",
        "Smartphone desk holder": "Masaüstü akıllı telefon tutucu stand",
        "Simple X shaped frame to support a laptop on a given angle": "Belirli açıda dizüstü bilgisayar (laptop) yükseltici stand",
    }

    lines = [
        'msgid ""',
        'msgstr ""',
        '"Project-Id-Version: boxes.py 1.2\\n"',
        '"Language: tr\\n"',
        '"MIME-Version: 1.0\\n"',
        '"Content-Type: text/plain; charset=UTF-8\\n"',
        '"Content-Transfer-Encoding: 8bit\\n"',
        ""
    ]
    for k, v in tr_dict.items():
        k_esc = k.replace('"', '\\"').replace('\n', '\\n')
        v_esc = v.replace('"', '\\"').replace('\n', '\\n')
        lines.append(f'msgid "{k_esc}"')
        lines.append(f'msgstr "{v_esc}"')
        lines.append("")

    tr_po = repo_po_dir / "tr.po"
    tr_po.write_text("\n".join(lines), encoding="utf-8")

def main():
    repo_dir = Path(r"c:\Users\otoem\Downloads\antigravity\boxes_py_app\boxes_repo")
    payload_dir = Path(r"c:\Users\otoem\Downloads\antigravity\boxes_py_app\payload")
    po_dir = repo_dir / "po"
    generate_tr_po(po_dir)

    for po_file in po_dir.glob("*.po"):
        lang = po_file.stem.replace("-", "_")
        mo_file = payload_dir / "locale" / lang / "LC_MESSAGES" / "boxes.py.mo"
        compile_po_to_mo(po_file, mo_file)
        print(f"Compiled {po_file.name} -> {mo_file}")

if __name__ == "__main__":
    main()
