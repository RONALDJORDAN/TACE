import base64
import tkinter as tk
from tkinter import ttk, filedialog, messagebox

import qrcode
from PIL import Image, ImageTk

APP_TITLE = "Gerador de QR Code - Comprovante Offline"


def build_receipt_html(nome, cpf, rd, nascimento):
    return f"""<!DOCTYPE html>
<html lang="pt-BR">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>Comprovante</title>
<style>
body{{font-family:Arial,sans-serif;margin:0;padding:24px;background:#fff;color:#000}}
.card{{max-width:420px;margin:0 auto;background:#fff;border:1px solid #000;border-radius:8px;padding:24px}}
h1{{margin:0 0 16px;font-size:20px;color:#000}}
.row{{display:flex;justify-content:space-between;padding:8px 0;border-bottom:1px solid #ccc}}
.label{{color:#444;font-size:12px}}
.value{{font-weight:bold;font-size:14px;color:#000}}
</style>
</head>
<body>
<div class="card">
<h1>Comprovante Offline</h1>
<div class="row"><span class="label">Nome</span><span class="value">{nome}</span></div>
<div class="row"><span class="label">CPF</span><span class="value">{cpf}</span></div>
<div class="row"><span class="label">RD</span><span class="value">{rd}</span></div>
<div class="row"><span class="label">Nascimento</span><span class="value">{nascimento}</span></div>
</div>
</body>
</html>"""


def make_qr_image(nome, cpf, rd, nascimento, html_mode=False):
    if html_mode:
        html = build_receipt_html(nome, cpf, rd, nascimento)
        payload = "data:text/html;base64," + base64.b64encode(html.encode("utf-8")).decode("ascii")
    else:
        payload = f"COMPROVANTE OFFLINE\nNome: {nome}\nCPF: {cpf}\nRD: {rd}\nNascimento: {nascimento}"
    qr = qrcode.QRCode(error_correction=qrcode.constants.ERROR_CORRECT_L, box_size=8, border=4)
    qr.add_data(payload)
    qr.make(fit=True)
    return qr.make_image(fill_color="black", back_color="white").convert("RGB")


class App(tk.Tk):
    def __init__(self):
        super().__init__()
        self.title(APP_TITLE)
        self.geometry("760x360")
        self.resizable(False, False)

        left = ttk.Frame(self, padding=20)
        left.pack(side="left", fill="y")

        fields = [("Nome", "nome"), ("CPF", "cpf"), ("RD", "rd"), ("Data de nascimento", "nasc")]
        self.vars = {}
        for i, (label, key) in enumerate(fields):
            ttk.Label(left, text=label).grid(row=i, column=0, sticky="w", pady=6)
            v = tk.StringVar()
            self.vars[key] = v
            ttk.Entry(left, textvariable=v, width=34).grid(row=i, column=1, pady=6)

        self.html_mode = tk.BooleanVar(value=False)
        ttk.Checkbutton(left, text="Transmitir como HTML (base64)", variable=self.html_mode).grid(
            row=len(fields), column=0, columnspan=2, sticky="w", pady=4
        )

        btns = ttk.Frame(left)
        btns.grid(row=len(fields) + 1, column=0, columnspan=2, pady=16)
        ttk.Button(btns, text="Pré-visualizar", command=self.preview).pack(side="left", padx=4)
        ttk.Button(btns, text="Gerar e salvar", command=self.on_generate).pack(side="left", padx=4)

        self.status = ttk.Label(left, text="", foreground="#513BA6", wraplength=320)
        self.status.grid(row=len(fields) + 2, column=0, columnspan=2)

        right = ttk.Frame(self, padding=20)
        right.pack(side="right", fill="both", expand=True)
        ttk.Label(right, text="Pré-visualização").pack()
        self.preview_label = ttk.Label(right)
        self.preview_label.pack(pady=10)
        self._photo = None

    def _get_values(self):
        return (
            self.vars["nome"].get().strip(),
            self.vars["cpf"].get().strip(),
            self.vars["rd"].get().strip(),
            self.vars["nasc"].get().strip(),
        )

    def preview(self):
        nome, cpf, rd, nasc = self._get_values()
        if not all([nome, cpf, rd, nasc]):
            messagebox.showwarning("Campos obrigatórios", "Preencha todos os campos.")
            return
        try:
            img = make_qr_image(nome, cpf, rd, nasc, self.html_mode.get())
        except Exception as e:
            messagebox.showerror("Erro", str(e))
            return
        img = img.resize((280, 280))
        self._photo = ImageTk.PhotoImage(img)
        self.preview_label.config(image=self._photo)
        self.status.config(text="Pré-visualização atualizada.")

    def on_generate(self):
        nome, cpf, rd, nasc = self._get_values()
        if not all([nome, cpf, rd, nasc]):
            messagebox.showwarning("Campos obrigatórios", "Preencha todos os campos.")
            return
        path = filedialog.asksaveasfilename(
            defaultextension=".png",
            filetypes=[("PNG", "*.png")],
            initialfile="comprovante_qr.png",
        )
        if not path:
            return
        try:
            img = make_qr_image(nome, cpf, rd, nasc, self.html_mode.get())
            img.save(path)
        except Exception as e:
            messagebox.showerror("Erro", str(e))
            return
        self.status.config(text=f"QR Code salvo em: {path}")
        preview = img.resize((280, 280))
        self._photo = ImageTk.PhotoImage(preview)
        self.preview_label.config(image=self._photo)


if __name__ == "__main__":
    App().mainloop()
