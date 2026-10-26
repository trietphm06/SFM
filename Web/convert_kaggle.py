#!/usr/bin/env python3
"""
Script chuyển đổi dataset FIFA/PES tải từ Kaggle sang chuẩn Simple Football Manager (SFM).
Đọc trực tiếp file .csv hoặc archive.zip, chỉ lấy male player base cards FIFA 23 + FIFA 22.
Chạy: python3 convert_kaggle.py <duong_dan_file_kaggle.csv|archive.zip>
"""

import sys
import os
import csv
import json
import re
import heapq
import hashlib
import zipfile
from contextlib import ExitStack

PLAYER_FILES_IN_ARCHIVE = ("male_players.csv",)
TARGET_BASE_SETS = ((23, 1), (22, 1))

def iter_csv_sources(input_path):
    if zipfile.is_zipfile(input_path):
        stack = ExitStack()
        archive = stack.enter_context(zipfile.ZipFile(input_path))
        names = [name for name in PLAYER_FILES_IN_ARCHIVE if name in archive.namelist()]
        if not names:
            names = [name for name in archive.namelist() if name.endswith(".csv")]

        with stack:
            for name in names:
                raw = stack.enter_context(archive.open(name))
                text = stack.enter_context(open_text_stream(raw))
                yield name, text
    else:
        with open(input_path, mode='r', encoding='utf-8', errors='ignore', newline='') as f:
            yield os.path.basename(input_path), f

def open_text_stream(raw):
    import io
    return io.TextIOWrapper(raw, encoding='utf-8', errors='ignore', newline='')

def to_int(value, default=0):
    try:
        if value in (None, ""):
            return default
        return int(float(value))
    except (TypeError, ValueError):
        return default

def version_num(value):
    try:
        return int(float(value or 0))
    except (TypeError, ValueError):
        return 0



# Thứ tự cột: 0 Name,1 Pos,2 AltPos,3 Year,4 Season,5-10 PAC,SHO,PAS,DRI,DEF,PHY,11-16 DIV,HAN,KIC,REF,SPE,POS,17 IQ,18 STA
GK_STAT_COLS = range(11, 17)
OUTFIELD_STAT_COLS = range(5, 10)  # PAC..DEF (PHY giữ nguyên vì thủ môn vẫn có Physical)

def clean_row(row):
    """Cầu thủ sân: chỉ số thủ môn = 0. Thủ môn: chỉ số 5 chỉ số sân (PAC..DEF) = 0."""
    row = list(row)
    if str(row[1]).strip().upper() == "GK":
        for i in OUTFIELD_STAT_COLS:
            row[i] = 0
    else:
        for i in GK_STAT_COLS:
            row[i] = 0
    return row

CSV_HEADER = ["Name","Position","AltPos","Year","Season","Pace","Shooting","Passing","Dribbling","Defending","Physical","Diving","Handling","Kicking","Reflexing","Speed","Positioning","IQ","Stamina"]

def inject_into_html(json_rows, html_path="index.html"):
    """Nhúng dữ liệu vào index.html (giữa 2 marker DB_DATA) để mở file HTML là có đủ cầu thủ, không cần fetch."""
    if not os.path.exists(html_path):
        print(f"⚠️ Không thấy {html_path}, bỏ qua bước nhúng vào HTML.")
        return False
    with open(html_path, encoding='utf-8') as f:
        html = f.read()
    if "// <<DB_DATA_START>>" not in html:
        print(f"⚠️ {html_path} chưa có marker DB_DATA, bỏ qua.")
        return False

    payload = json.dumps(json_rows, ensure_ascii=False, separators=(',', ':'))
    version = hashlib.md5(payload.encode('utf-8')).hexdigest()[:10]
    lines = ",\n".join("    " + json.dumps(r, ensure_ascii=False, separators=(',', ':')) for r in json_rows)
    block = (
        "// <<DB_DATA_START>>\n"
        f'const DB_VERSION = "{version}";\n'
        f"let defaultDB = [\n{lines}\n];\n"
        "// <<DB_DATA_END>>"
    )
    new_html, n = re.subn(r"// <<DB_DATA_START>>.*?// <<DB_DATA_END>>", lambda m: block, html, count=1, flags=re.S)
    if n != 1:
        print("⚠️ Không thay được block dữ liệu trong HTML.")
        return False
    with open(html_path, mode='w', encoding='utf-8') as f:
        f.write(new_html)
    print(f"🌐 Đã nhúng {len(json_rows)} cầu thủ vào {html_path} (DB_VERSION={version})")
    return True

def convert_kaggle_fifa(input_path, output_csv="PlayersData.csv", output_json="sfm_database.json", limit_per_set=120, html_path="index.html"):
    if not os.path.exists(input_path):
        print(f"❌ Không tìm thấy file: {input_path}")
        return

    print(f"📂 Đang đọc dữ liệu từ: {input_path} ...")

    players_by_set = {target: {} for target in TARGET_BASE_SETS}
    rows_read = 0

    for source_name, f in iter_csv_sources(input_path):
        print(f"  • Đang xử lý: {source_name}")
        reader = csv.DictReader(f)
        for row in reader:
            rows_read += 1
            # Lấy tên
            name = row.get('short_name') or row.get('Name') or row.get('name') or row.get('Known As') or 'Unknown'
            if not name or name == 'Unknown':
                continue

            ovr = to_int(row.get('overall') or row.get('Overall') or row.get('OVR'), 0)
            if ovr <= 0:
                continue

            player_id = row.get('player_id') or row.get('sofifa_id') or name
            version = str(row.get('fifa_version') or row.get('year') or '2024')
            update = row.get('fifa_update')
            target = (version_num(version), version_num(update))
            if target not in players_by_set:
                continue
            
            # Vị trí
            positions_raw = row.get('player_positions') or row.get('Position') or row.get('Best Position') or row.get('positions') or 'CM'
            pos_list = [p.strip().upper() for p in positions_raw.split(',') if p.strip()]
            main_pos = pos_list[0] if pos_list else 'CM'
            alt_pos = ','.join(pos_list[1:3]) if len(pos_list) > 1 else ''
            
            # Năm & Mùa
            year = version
            if year and len(str(year)) <= 2: year = f"20{year}"
            season = 'Base'

            # Outfield stats
            pac = to_int(row.get('pace') or row.get('Pace') or row.get('PAC'), 0 if main_pos == 'GK' else 70)
            sho = to_int(row.get('shooting') or row.get('Shooting') or row.get('SHO'), 0 if main_pos == 'GK' else 65)
            pas = to_int(row.get('passing') or row.get('Passing') or row.get('PAS'), 0 if main_pos == 'GK' else 70)
            dri = to_int(row.get('dribbling') or row.get('Dribbling') or row.get('DRI'), 0 if main_pos == 'GK' else 70)
            defe = to_int(row.get('defending') or row.get('Defending') or row.get('DEF'), 0 if main_pos == 'GK' else 60)
            phy = to_int(row.get('physic') or row.get('Physical') or row.get('PHY'), 55 if main_pos == 'GK' else 70)

            # GK stats
            div = to_int(row.get('goalkeeping_diving') or row.get('GK Diving') or row.get('DIV'), 0)
            han = to_int(row.get('goalkeeping_handling') or row.get('GK Handling') or row.get('HAN'), 0)
            kic = to_int(row.get('goalkeeping_kicking') or row.get('GK Kicking') or row.get('KIC'), 0)
            ref = to_int(row.get('goalkeeping_reflexes') or row.get('GK Reflexes') or row.get('REF'), 0)
            spe = to_int(row.get('goalkeeping_speed') or row.get('GK Speed') or row.get('SPE'), 0)
            pos_gk = to_int(row.get('goalkeeping_positioning') or row.get('GK Positioning') or row.get('POS'), 0)

            # IQ & Stamina
            # Composure / Reactions làm IQ
            iq = to_int(row.get('movement_reactions') or row.get('mentality_composure') or row.get('Reactions'), 75)
            # Stamina
            sta = to_int(row.get('power_stamina') or row.get('Stamina'), 80)

            player_row = [name, main_pos, alt_pos, str(year), season, pac, sho, pas, dri, defe, phy, div, han, kic, ref, spe, pos_gk, iq, sta, ovr]
            current_best = players_by_set[target].get(player_id)
            candidate = (version_num(version), ovr, name, player_row)
            if current_best is None or candidate[:3] > current_best[:3]:
                players_by_set[target][player_id] = candidate

    selected = []
    for target in TARGET_BASE_SETS:
        players_heap = []
        for version, ovr, name, player_row in players_by_set[target].values():
            item = (ovr, name, player_row)
            if len(players_heap) < limit_per_set:
                heapq.heappush(players_heap, item)
            elif item > players_heap[0]:
                heapq.heapreplace(players_heap, item)

        selected.extend(item[2] for item in sorted(players_heap, reverse=True))

    # Dọn chỉ số thừa (cầu thủ sân không có chỉ số thủ môn)
    selected = [clean_row(p[:-1]) + [p[-1]] for p in selected]

    # Ghi ra PlayersData.csv
    with open(output_csv, mode='w', encoding='utf-8', newline='') as f:
        writer = csv.writer(f)
        writer.writerow(CSV_HEADER)
        for p in selected:
            writer.writerow(p[:-1])

    # Ghi ra sfm_database.json để nạp thẳng vào Web
    json_rows = [p[:-1] for p in selected]
    with open(output_json, mode='w', encoding='utf-8') as f:
        json.dump(json_rows, f, ensure_ascii=False, indent=2)

    inject_into_html(json_rows, html_path)

    target_names = ", ".join(f"FIFA {version}" for version, update in TARGET_BASE_SETS)
    print(f"✅ Đã đọc {rows_read} dòng và chuyển đổi thành công {len(selected)} male base cards ({target_names})!")
    print(f"📁 Xuất ra: {output_csv} và {output_json}")

if __name__ == '__main__':
    if len(sys.argv) > 1 and sys.argv[1] == "--clean-only":
        # Dọn chỉ số thủ môn thừa trong sfm_database.json + PlayersData.csv có sẵn rồi nhúng lại vào HTML
        with open("sfm_database.json", encoding='utf-8') as f:
            rows = [clean_row(r) for r in json.load(f)]
        with open("sfm_database.json", mode='w', encoding='utf-8') as f:
            json.dump(rows, f, ensure_ascii=False, indent=2)
        with open("PlayersData.csv", mode='w', encoding='utf-8', newline='') as f:
            w = csv.writer(f)
            w.writerow(CSV_HEADER)
            w.writerows(rows)
        print(f"🧹 Đã dọn {len(rows)} dòng (sfm_database.json + PlayersData.csv)")
        inject_into_html(rows)
    elif len(sys.argv) > 1 and sys.argv[1] == "--inject-only":
        # Chỉ nhúng lại sfm_database.json có sẵn vào index.html
        with open("sfm_database.json", encoding='utf-8') as f:
            inject_into_html(json.load(f))
    elif len(sys.argv) > 1:
        convert_kaggle_fifa(sys.argv[1])
    else:
        print("Sử dụng: python3 convert_kaggle.py <duong_dan_file_kaggle.csv|archive.zip>  hoặc  --clean-only | --inject-only")
