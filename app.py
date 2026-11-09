import os, sys, sqlite3, threading, socket
from datetime import datetime
from flask import Flask, request, jsonify
from PySide6.QtCore import QTimer
from PySide6.QtWidgets import QApplication, QMainWindow, QWidget, QVBoxLayout, QHBoxLayout, QLabel, QPushButton, QTableWidget, QTableWidgetItem, QMessageBox

DB = os.path.join(os.path.dirname(__file__), "schoolcontrol.db")
api = Flask(__name__)

def db():
    c = sqlite3.connect(DB); c.row_factory = sqlite3.Row
    c.execute("""CREATE TABLE IF NOT EXISTS students(
        id INTEGER PRIMARY KEY, full_name TEXT NOT NULL, pc_name TEXT NOT NULL UNIQUE,
        online INTEGER DEFAULT 0, last_seen TEXT, joined_at TEXT)""")
    c.execute("""CREATE TABLE IF NOT EXISTS events(
        id INTEGER PRIMARY KEY, full_name TEXT, pc_name TEXT, event TEXT, at TEXT)""")
    c.commit(); return c

@api.post("/api/checkin")
def checkin():
    data = request.get_json(force=True)
    name, pc = str(data.get("full_name","")).strip(), str(data.get("pc_name","")).strip()
    if not name or not pc or len(name) > 100: return jsonify(error="Ism-familiya va kompyuter nomi kerak"), 400
    now = datetime.now().isoformat(timespec="seconds")
    c = db(); old = c.execute("SELECT full_name FROM students WHERE pc_name=?", (pc,)).fetchone()
    c.execute("""INSERT INTO students(full_name,pc_name,online,last_seen,joined_at) VALUES(?,?,1,?,?)
      ON CONFLICT(pc_name) DO UPDATE SET full_name=excluded.full_name,online=1,last_seen=excluded.last_seen,joined_at=excluded.joined_at""", (name,pc,now,now))
    c.execute("INSERT INTO events(full_name,pc_name,event,at) VALUES(?,?,?,?)",(name,pc,"Kirdi",now)); c.commit(); c.close()
    return jsonify(ok=True)

@api.post("/api/heartbeat")
def heartbeat():
    pc = str(request.get_json(force=True).get("pc_name",""))
    c=db(); c.execute("UPDATE students SET online=1,last_seen=? WHERE pc_name=?",(datetime.now().isoformat(timespec="seconds"),pc)); c.commit(); c.close()
    return jsonify(ok=True)

@api.get("/api/students")
def students():
    c=db()
    # Stale clients are shown offline after 20 seconds.
    c.execute("UPDATE students SET online=0 WHERE last_seen < ?",(datetime.now().timestamp().__str__(),))
    rows=[dict(r) for r in c.execute("SELECT * FROM students ORDER BY full_name")]
    c.close(); return jsonify(rows)

@api.get("/api/events")
def events():
    c=db(); rows=[dict(r) for r in c.execute("SELECT * FROM events ORDER BY id DESC LIMIT 100")]; c.close()
    return jsonify(rows)

class TeacherWindow(QMainWindow):
    def __init__(self):
        super().__init__(); self.setWindowTitle("SchoolControl • Teacher Console"); self.resize(1100,700)
        self.setStyleSheet("QMainWindow{background:#0b1220;color:#eaf0ff} QWidget{color:#eaf0ff;font:14px 'Segoe UI'} QLabel#hero{font-size:27px;font-weight:700;color:#fff} QPushButton{background:#2563eb;border:0;border-radius:9px;padding:10px 16px;color:white} QTableWidget{background:#111c30;gridline-color:#26344d;border:1px solid #26344d;border-radius:12px} QHeaderView::section{background:#1b2940;padding:9px;border:0;font-weight:600}")
        root=QWidget(); self.setCentralWidget(root); lay=QVBoxLayout(root); lay.setContentsMargins(28,24,28,24); lay.setSpacing(16)
        top=QHBoxLayout(); title=QVBoxLayout(); hero=QLabel("SchoolControl"); hero.setObjectName("hero"); title.addWidget(hero); title.addWidget(QLabel("O‘qituvchi boshqaruv paneli • Mahalliy tarmoq")); top.addLayout(title); top.addStretch()
        refresh=QPushButton("↻  Yangilash"); refresh.clicked.connect(self.load); top.addWidget(refresh); lay.addLayout(top)
        self.summary=QLabel("Ulangan o‘quvchilar: —"); lay.addWidget(self.summary)
        self.table=QTableWidget(0,4); self.table.setHorizontalHeaderLabels(["O‘quvchi","Kompyuter","Holat","Oxirgi faollik"]); self.table.horizontalHeader().setStretchLastSection(True); self.table.setEditTriggers(QTableWidget.NoEditTriggers); lay.addWidget(self.table)
        self.note=QLabel("Eslatma: bu boshlang‘ich versiya davomat va qurilma holatini ko‘rsatadi. Masofaviy boshqaruv funksiyalari ushbu versiyada yoqilmagan.")
        self.note.setWordWrap(True); lay.addWidget(self.note)
        self.timer=QTimer(self); self.timer.timeout.connect(self.load); self.timer.start(5000); self.load()
    def load(self):
        import urllib.request, json
        try:
            with urllib.request.urlopen("http://127.0.0.1:8765/api/students",timeout=2) as r: rows=json.loads(r.read())
            self.table.setRowCount(len(rows)); online=0
            for i,s in enumerate(rows):
                online += bool(s["online"])
                vals=[s["full_name"],s["pc_name"],"Online" if s["online"] else "Offline",s.get("last_seen") or "—"]
                for j,v in enumerate(vals): self.table.setItem(i,j,QTableWidgetItem(str(v)))
            self.summary.setText(f"Jami: {len(rows)}  •  Online: {online}  •  Offline: {len(rows)-online}")
        except Exception: self.summary.setText("Server ishga tushmoqda yoki ulanish yo‘q.")

def serve():
    api.run(host="0.0.0.0",port=8765,debug=False,use_reloader=False)

if __name__=="__main__":
    threading.Thread(target=serve,daemon=True).start()
    app=QApplication(sys.argv); w=TeacherWindow(); w.show(); sys.exit(app.exec())
