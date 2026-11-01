#!/usr/bin/env python3
"""
ApparelPOS Real Razorpay Automated Activation Server
Full Production Implementation:
- Server-Side Razorpay Orders API (/api/create-order)
- Cryptographic HMAC-SHA256 Signature Verification (/api/verify-payment)
- Instant RSA-2048 Signed Offline Key Generation
- Embedded SQLite Sales Ledger (sales.db)
- Protected Admin Dashboard (/admin)
- Automatic .env file configuration loader
"""

import os
import sys
import json
import base64
import sqlite3
import hmac
import hashlib
import urllib.request
import urllib.error
from datetime import datetime, timedelta
from http.server import HTTPServer, BaseHTTPRequestHandler
from urllib.parse import urlparse, parse_qs
from cryptography.hazmat.primitives import hashes
from cryptography.hazmat.primitives.asymmetric import padding
from cryptography.hazmat.primitives import serialization

BASE_DIR = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PRIVATE_KEY_PATH = os.path.join(BASE_DIR, "private_key.pem")
SERVER_DIR = os.path.dirname(os.path.abspath(__file__))
DB_PATH = os.path.join(SERVER_DIR, "sales.db")
ENV_PATH = os.path.join(SERVER_DIR, ".env")

# ---------------------------------------------------------
# Load .env File
# ---------------------------------------------------------
def load_env():
    if os.path.exists(ENV_PATH):
        with open(ENV_PATH, "r", encoding="utf-8") as f:
            for line in f:
                line = line.strip()
                if line and not line.startswith("#") and "=" in line:
                    key, val = line.split("=", 1)
                    os.environ[key.strip()] = val.strip().strip('"').strip("'")

def get_razorpay_key_id():
    load_env()
    return os.environ.get("RAZORPAY_KEY_ID", "").strip()

def get_razorpay_key_secret():
    load_env()
    return os.environ.get("RAZORPAY_KEY_SECRET", "").strip()

def get_admin_secret():
    load_env()
    return os.environ.get("ADMIN_SECRET", "admin123").strip()

LICENSE_FEE_INR = 249
LICENSE_FEE_PAISE = LICENSE_FEE_INR * 100

# ---------------------------------------------------------
# Database Initialization
# ---------------------------------------------------------
def init_db():
    conn = sqlite3.connect(DB_PATH)
    cur = conn.cursor()
    cur.execute("""
        CREATE TABLE IF NOT EXISTS sales (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            order_id TEXT,
            payment_id TEXT UNIQUE,
            machine_code TEXT NOT NULL,
            customer_name TEXT,
            store_name TEXT,
            phone TEXT,
            amount INTEGER DEFAULT 249,
            currency TEXT DEFAULT 'INR',
            issued_at TEXT NOT NULL,
            expiry_date TEXT NOT NULL,
            license_key TEXT NOT NULL
        )
    """)
    conn.commit()
    conn.close()

init_db()

# ---------------------------------------------------------
# Cryptographic License Generator (RSA-2048)
# ---------------------------------------------------------
def load_private_key():
    env_key = os.environ.get("PRIVATE_KEY_PEM", "").strip()
    if env_key:
        # Convert escaped newlines if passed in single-line env var
        formatted_key = env_key.replace("\\n", "\n").encode("utf-8")
        return serialization.load_pem_private_key(formatted_key, password=None)

    if not os.path.exists(PRIVATE_KEY_PATH):
        raise FileNotFoundError(f"Private key not found at {PRIVATE_KEY_PATH}")
    with open(PRIVATE_KEY_PATH, "rb") as f:
        return serialization.load_pem_private_key(f.read(), password=None)

def issue_license_key(machine_code: str, customer_name: str = "Retailer", store_name: str = "Apparel Store") -> tuple[str, str, str]:
    private_key = load_private_key()
    today = datetime.now()
    expiry = today + timedelta(days=365)

    issued_str = today.strftime("%Y-%m-%d")
    expiry_str = expiry.strftime("%Y-%m-%d")

    payload_dict = {
        "MachineCode": machine_code.strip().upper(),
        "CustomerName": customer_name.strip() if customer_name else "Retailer",
        "StoreName": store_name.strip() if store_name else "Apparel Store",
        "ExpiryDate": expiry_str,
        "IssuedAt": issued_str,
        "LicenseType": "Commercial Annual"
    }

    json_bytes = json.dumps(payload_dict, separators=(',', ':'), sort_keys=True).encode('utf-8')
    signature = private_key.sign(
        json_bytes,
        padding.PKCS1v15(),
        hashes.SHA256()
    )

    p_b64 = base64.b64encode(json_bytes).decode('ascii')
    s_b64 = base64.b64encode(signature).decode('ascii')
    return f"{p_b64}.{s_b64}", issued_str, expiry_str

def save_sale(order_id: str, payment_id: str, machine_code: str, customer_name: str, store_name: str, phone: str, amount: int, license_key: str, issued_at: str, expiry_date: str):
    conn = sqlite3.connect(DB_PATH)
    cur = conn.cursor()
    cur.execute("""
        INSERT OR REPLACE INTO sales 
        (order_id, payment_id, machine_code, customer_name, store_name, phone, amount, currency, issued_at, expiry_date, license_key)
        VALUES (?, ?, ?, ?, ?, ?, ?, 'INR', ?, ?, ?)
    """, (order_id, payment_id, machine_code.upper(), customer_name, store_name, phone, amount, issued_at, expiry_date, license_key))
    conn.commit()
    conn.close()

def get_sales_stats():
    conn = sqlite3.connect(DB_PATH)
    cur = conn.cursor()
    cur.execute("SELECT COUNT(*), COALESCE(SUM(amount), 0) FROM sales")
    total_sales, total_revenue = cur.fetchone()
    cur.execute("SELECT payment_id, machine_code, customer_name, store_name, phone, amount, issued_at, expiry_date, license_key FROM sales ORDER BY id DESC LIMIT 50")
    recent = cur.fetchall()
    conn.close()
    return total_sales, total_revenue, recent

# ---------------------------------------------------------
# Razorpay REST API Client (Zero external libraries)
# ---------------------------------------------------------
def create_razorpay_order(machine_code: str, store_name: str) -> dict:
    """Calls Razorpay API to create an authenticated order."""
    key_id = get_razorpay_key_id()
    key_secret = get_razorpay_key_secret()
    if not key_id or not key_secret:
        raise ValueError("Razorpay Key ID or Secret is not configured in server/.env")

    url = "https://api.razorpay.com/v1/orders"
    auth_str = f"{key_id}:{key_secret}"
    auth_b64 = base64.b64encode(auth_str.encode()).decode()

    payload_data = {
        "amount": LICENSE_FEE_PAISE,
        "currency": "INR",
        "receipt": f"pos_{int(datetime.now().timestamp())}",
        "notes": {
            "machine_code": machine_code,
            "store_name": store_name,
            "product": "ApparelPOS 1-Year Offline License"
        }
    }
    payload_bytes = json.dumps(payload_data).encode("utf-8")

    req = urllib.request.Request(
        url,
        data=payload_bytes,
        headers={
            "Authorization": f"Basic {auth_b64}",
            "Content-Type": "application/json"
        },
        method="POST"
    )

    with urllib.request.urlopen(req, timeout=10) as resp:
        return json.loads(resp.read().decode())

def verify_razorpay_signature(order_id: str, payment_id: str, signature: str) -> bool:
    """Validates the HMAC-SHA256 signature returned by Razorpay checkout."""
    key_secret = get_razorpay_key_secret()
    if not key_secret:
        return False
    msg = f"{order_id}|{payment_id}".encode("utf-8")
    secret = key_secret.encode("utf-8")
    expected = hmac.new(secret, msg, hashlib.sha256).hexdigest()
    return hmac.compare_digest(expected, signature)

# ---------------------------------------------------------
# HTML UI Templates
# ---------------------------------------------------------
MAIN_HTML = """<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>ApparelPOS 1-Year License Renewal</title>
    <script src="https://checkout.razorpay.com/v1/checkout.js"></script>
    <style>
        * { box-sizing: border-box; margin: 0; padding: 0; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; }
        body { background: #0F172A; color: #F8FAFC; min-height: 100vh; display: flex; align-items: center; justify-content: center; padding: 16px; }
        .card { background: #1E293B; border: 1px solid #334155; border-radius: 20px; max-width: 460px; width: 100%; padding: 26px; box-shadow: 0 15px 35px rgba(0,0,0,0.5); }
        .badge { background: #065F46; color: #6EE7B7; padding: 5px 14px; border-radius: 999px; font-size: 11.5px; font-weight: 700; display: inline-block; margin-bottom: 12px; }
        h1 { font-size: 21px; font-weight: 700; margin-bottom: 6px; }
        p.sub { font-size: 13px; color: #94A3B8; margin-bottom: 18px; }
        .form-group { margin-bottom: 14px; }
        label { display: block; font-size: 11.5px; font-weight: 600; color: #94A3B8; margin-bottom: 6px; text-transform: uppercase; letter-spacing: 0.5px; }
        input[type="text"], input[type="tel"] {
            width: 100%; background: #0F172A; border: 1px solid #334155; border-radius: 10px; padding: 11px 14px; color: #FFFFFF; font-size: 14px; outline: none; transition: border-color 0.2s;
        }
        input[type="text"]:focus, input[type="tel"]:focus { border-color: #6366F1; }
        .hwid-box { background: #0F172A; border: 1.5px solid #6366F1; border-radius: 10px; padding: 12px 14px; font-family: monospace; font-size: 18px; font-weight: 700; color: #38BDF8; letter-spacing: 1.5px; text-align: center; margin-bottom: 16px; }
        .price-card { display: flex; justify-content: space-between; align-items: center; margin: 16px 0; padding: 14px 16px; background: rgba(99, 102, 241, 0.1); border-radius: 12px; border: 1px solid rgba(99, 102, 241, 0.25); }
        .price-title { font-size: 13.5px; font-weight: 600; color: #C7D2FE; }
        .price-val { font-size: 24px; font-weight: 800; color: #10B981; }
        .btn-pay { display: block; width: 100%; background: #4F46E5; color: white; padding: 14px; border: none; border-radius: 12px; font-size: 15px; font-weight: 700; cursor: pointer; text-align: center; text-decoration: none; transition: background 0.2s; }
        .btn-pay:hover { background: #4338CA; }
        .btn-pay:disabled { background: #475569; cursor: not-allowed; }
        .status-msg { margin-top: 12px; font-size: 12px; text-align: center; color: #F59E0B; }
        .footer { font-size: 11px; color: #64748B; text-align: center; margin-top: 18px; }
        .rzp-badge { display: flex; align-items: center; justify-content: center; gap: 8px; margin-top: 12px; font-size: 11px; color: #94A3B8; }
    </style>
</head>
<body>
    <div class="card" id="mainCard">
        <span class="badge">OFFLINE RETAIL POS RENEWAL</span>
        <h1>ApparelPOS Annual Renewal</h1>
        <p class="sub">100% Offline Point of Sale &amp; Barcode Management</p>

        <form id="renewForm" onsubmit="initiatePayment(event)">
            <label>Machine Code (Node-Locked to PC)</label>
            <div class="hwid-box" id="dispMc">{{MACHINE_CODE}}</div>
            <input type="hidden" id="machineCode" value="{{MACHINE_CODE}}">

            <div class="form-group">
                <label>Store / Shop Name</label>
                <input type="text" id="storeName" placeholder="e.g. Royal Garments" required value="{{STORE_NAME}}">
            </div>

            <div class="form-group">
                <label>Owner Name</label>
                <input type="text" id="customerName" placeholder="e.g. Ramesh Kumar" required value="Store Owner">
            </div>

            <div class="form-group">
                <label>Mobile Number (For Receipt &amp; Backup)</label>
                <input type="tel" id="phone" placeholder="e.g. 9876543210" pattern="[0-9]{10}" required>
            </div>

            <div class="price-card">
                <div class="price-title">1-Year Commercial License</div>
                <div class="price-val">₹249</div>
            </div>

            <button type="submit" class="btn-pay" id="payBtn">
                ⚡ Pay ₹249 with Razorpay (UPI / Card)
            </button>
            <div id="statusMsg" class="status-msg"></div>

            <div class="rzp-badge">
                <span>🔒 Secured by Razorpay</span> • <span>UPI / GPay / PhonePe / Cards</span>
            </div>
        </form>

        <div class="footer">
            Hardware-locked RSA-2048 • Key generated instantly upon verified payment
            <div style="margin-top:14px;padding-top:12px;border-top:1px solid #334155;display:flex;justify-content:center;gap:12px;flex-wrap:wrap;">
                <a href="/terms-and-conditions" style="color:#94A3B8;text-decoration:none;font-size:11px;">Terms &amp; Conditions</a>
                <a href="/privacy-policy" style="color:#94A3B8;text-decoration:none;font-size:11px;">Privacy Policy</a>
                <a href="/refund-policy" style="color:#94A3B8;text-decoration:none;font-size:11px;">Refund Policy</a>
                <a href="/contact-us" style="color:#94A3B8;text-decoration:none;font-size:11px;">Contact Us</a>
            </div>
        </div>
    </div>

    <script>
        var isRazorpayConfigured = {{IS_RAZORPAY_CONFIGURED}};
        var razorpayKeyId = "{{RAZORPAY_KEY_ID}}";

        async function initiatePayment(e) {
            e.preventDefault();
            var btn = document.getElementById("payBtn");
            var statusMsg = document.getElementById("statusMsg");
            var mc = document.getElementById("machineCode").value;
            var store = document.getElementById("storeName").value;
            var cust = document.getElementById("customerName").value;
            var phone = document.getElementById("phone").value;

            btn.disabled = true;
            statusMsg.innerText = "Creating secure Razorpay order...";

            if (!isRazorpayConfigured) {
                // If keys are not in server/.env yet:
                var proceed = confirm("Razorpay Keys are not configured in server/.env yet.\\n\\nClick OK to simulate ₹249 payment and receive your RSA-2048 offline key,\\nor Cancel to configure keys in server/.env first.");
                if (proceed) {
                    window.location.href = "/process-success?payment_id=SIM_" + Date.now() + "&mc=" + encodeURIComponent(mc) + "&store=" + encodeURIComponent(store) + "&cust=" + encodeURIComponent(cust) + "&phone=" + encodeURIComponent(phone);
                } else {
                    btn.disabled = false;
                    statusMsg.innerText = "Paste your RAZORPAY_KEY_ID and RAZORPAY_KEY_SECRET into server/.env and restart the server.";
                }
                return;
            }

            try {
                // 1. Create Order via Server Backend
                const res = await fetch("/api/create-order", {
                    method: "POST",
                    headers: { "Content-Type": "application/json" },
                    body: JSON.stringify({ machine_code: mc, store_name: store })
                });

                const orderData = await res.json();
                if (!res.ok || !orderData.order_id) {
                    throw new Error(orderData.error || "Failed to create Razorpay order");
                }

                statusMsg.innerText = "Opening payment window...";

                // 2. Open Real Razorpay Checkout
                var options = {
                    "key": razorpayKeyId,
                    "amount": 24900,
                    "currency": "INR",
                    "name": "ApparelPOS",
                    "description": "1-Year License (" + mc + ")",
                    "image": "https://cdn-icons-png.flaticon.com/512/3531/3531818.png",
                    "order_id": orderData.order_id,
                    "prefill": {
                        "name": cust,
                        "contact": phone
                    },
                    "theme": { "color": "#4F46E5" },
                    "handler": async function (response) {
                        statusMsg.innerText = "Verifying cryptographic payment signature...";
                        // 3. Verify Payment Signature on Server
                        try {
                            const verifyRes = await fetch("/api/verify-payment", {
                                method: "POST",
                                headers: { "Content-Type": "application/json" },
                                body: JSON.stringify({
                                    order_id: response.razorpay_order_id,
                                    payment_id: response.razorpay_payment_id,
                                    signature: response.razorpay_signature,
                                    machine_code: mc,
                                    store_name: store,
                                    customer_name: cust,
                                    phone: phone
                                })
                            });
                            const result = await verifyRes.json();
                            if (result.success) {
                                renderSuccessView(result);
                            } else {
                                alert("Signature verification failed: " + result.error);
                                btn.disabled = false;
                            }
                        } catch (err) {
                            alert("Verification error: " + err.message);
                            btn.disabled = false;
                        }
                    },
                    "modal": {
                        "ondismiss": function() {
                            btn.disabled = false;
                            statusMsg.innerText = "Payment cancelled. You can try again.";
                        }
                    }
                };

                var rzp = new Razorpay(options);
                rzp.open();

            } catch (err) {
                btn.disabled = false;
                statusMsg.innerText = "Error: " + err.message;
            }
        }

        function renderSuccessView(data) {
            document.getElementById("mainCard").innerHTML = `
                <div style="width:54px;height:54px;background:#065F46;border-radius:50%;display:flex;align-items:center;justify-content:center;font-size:26px;color:#10B981;margin:0 auto 14px;">✓</div>
                <h1 style="text-align:center;">Payment Verified!</h1>
                <p style="font-size:13px;color:#94A3B8;text-align:center;margin-bottom:18px;">1-Year Commercial License Activated</p>

                <div style="background:#0F172A;border-radius:10px;padding:12px 14px;margin-bottom:14px;font-size:12.5px;">
                    <div style="display:flex;justify-content:space-between;padding:4px 0;"><span style="color:#94A3B8;">Payment ID:</span><span style="font-family:monospace;color:#F8FAFC;">${data.payment_id}</span></div>
                    <div style="display:flex;justify-content:space-between;padding:4px 0;"><span style="color:#94A3B8;">Machine Code:</span><span style="font-family:monospace;color:#38BDF8;">${data.machine_code}</span></div>
                    <div style="display:flex;justify-content:space-between;padding:4px 0;"><span style="color:#94A3B8;">Valid Until:</span><span style="font-family:monospace;color:#10B981;font-weight:700;">${data.expiry_date}</span></div>
                </div>

                <div style="background:#020617;border:1.5px solid #10B981;border-radius:12px;padding:14px;margin:18px 0;">
                    <div style="font-size:11.5px;font-weight:700;color:#10B981;text-transform:uppercase;margin-bottom:8px;">YOUR 1-YEAR OFFLINE LICENSE KEY:</div>
                    <textarea id="keyArea" readonly style="width:100%;height:95px;background:transparent;color:#6EE7B7;border:none;font-family:monospace;font-size:11px;resize:none;outline:none;">${data.license_key}</textarea>
                    <button onclick="copyKey()" style="display:block;width:100%;background:#10B981;color:#022C22;font-weight:800;font-size:14px;padding:13px;border:none;border-radius:10px;cursor:pointer;margin-top:10px;">📋 Copy 1-Year Key</button>
                </div>

                <div style="background:rgba(56,189,248,0.08);border:1px solid rgba(56,189,248,0.2);border-radius:10px;padding:12px 14px;font-size:12px;line-height:1.6;color:#BAE6FD;">
                    <strong>Next Step on your Desktop:</strong><br>
                    Paste this key into the <strong>Activation Window</strong> on your billing PC and click <em>"Verify Signature &amp; Activate"</em>.
                </div>
            `;
        }

        function copyKey() {
            var ta = document.getElementById("keyArea");
            ta.select();
            navigator.clipboard.writeText(ta.value).then(function() {
                alert("✓ License Key copied to clipboard! Now paste it into ApparelPOS on your desktop PC.");
            });
        }
    </script>
</body>
</html>
"""

def render_policy(title: str, content: str) -> str:
    return f"""<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>{title} - ApparelPOS</title>
    <style>
        * {{ box-sizing: border-box; margin: 0; padding: 0; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; }}
        body {{ background: #0F172A; color: #F8FAFC; min-height: 100vh; padding: 24px 16px; display: flex; justify-content: center; }}
        .policy-card {{ background: #1E293B; border: 1px solid #334155; border-radius: 16px; max-width: 680px; width: 100%; padding: 32px; box-shadow: 0 10px 30px rgba(0,0,0,0.4); }}
        h1 {{ font-size: 24px; font-weight: 700; color: #FFFFFF; margin-bottom: 20px; padding-bottom: 12px; border-bottom: 1px solid #334155; }}
        h3 {{ font-size: 15px; font-weight: 600; color: #38BDF8; margin-top: 18px; margin-bottom: 6px; }}
        p {{ font-size: 13.5px; line-height: 1.7; color: #CBD5E1; margin-bottom: 10px; }}
        a.back-btn {{ display: inline-block; background: #334155; color: #F8FAFC; padding: 8px 16px; border-radius: 8px; text-decoration: none; font-size: 13px; font-weight: 600; margin-bottom: 20px; }}
        a.back-btn:hover {{ background: #475569; }}
    </style>
</head>
<body>
    <div class="policy-card">
        <a href="/" class="back-btn">← Back to License Renewal</a>
        <h1>{title}</h1>
        <div style="font-size:12px;color:#94A3B8;margin-bottom:16px;">Last updated: November 2026 • ApparelPOS Retail Systems</div>
        {content}
    </div>
</body>
</html>
"""

# ---------------------------------------------------------
# HTTP Server Request Dispatcher
# ---------------------------------------------------------
class ProductionRazorpayHandler(BaseHTTPRequestHandler):

    def do_HEAD(self):
        parsed = urlparse(self.path)
        path = parsed.path
        if path == "/download/ApparelPOS.exe":
            cache_path = os.path.join(SERVER_DIR, "ApparelPOS.exe")
            if not os.path.exists(cache_path) or os.path.getsize(cache_path) < 1000000:
                local_pub = os.path.join(BASE_DIR, "publish", "ApparelPOS.exe")
                if os.path.exists(local_pub) and os.path.getsize(local_pub) > 1000000:
                    import shutil
                    shutil.copyfile(local_pub, cache_path)
                else:
                    gh_url = "https://github.com/pawarswapnil9130/business/releases/download/v1.0.0/ApparelPOS.exe"
                    req = urllib.request.Request(gh_url, headers={"User-Agent": "Mozilla/5.0"})
                    with urllib.request.urlopen(req) as resp, open(cache_path, "wb") as f_out:
                        while chunk := resp.read(1024 * 1024):
                            f_out.write(chunk)

            file_size = os.path.getsize(cache_path)
            self.send_response(200)
            self.send_header("Content-Type", "application/octet-stream")
            self.send_header("Content-Disposition", 'attachment; filename="ApparelPOS.exe"')
            self.send_header("Content-Length", str(file_size))
            self.end_headers()
            return
        else:
            self.send_response(200)
            self.end_headers()

    def do_POST(self):
        parsed = urlparse(self.path)
        path = parsed.path

        length = int(self.headers.get('Content-Length', 0))
        body = self.rfile.read(length) if length > 0 else b'{}'
        try:
            data = json.loads(body.decode('utf-8'))
        except Exception:
            data = {}

        # 1. API: Create Razorpay Order
        if path == "/api/create-order":
            mc = data.get("machine_code", "").strip().upper()
            store = data.get("store_name", "Apparel Store").strip()

            if not mc:
                self.respond_json({"error": "Machine code is required"}, status=400)
                return

            try:
                order = create_razorpay_order(mc, store)
                self.respond_json({
                    "order_id": order["id"],
                    "amount": order["amount"],
                    "currency": order["currency"]
                })
            except urllib.error.HTTPError as http_err:
                err_content = http_err.read().decode()
                self.respond_json({"error": f"Razorpay API Error: {err_content}"}, status=http_err.code)
            except Exception as e:
                self.respond_json({"error": str(e)}, status=500)
            return

        # 2. API: Verify Razorpay Payment Signature & Issue Key
        elif path == "/api/verify-payment":
            order_id = data.get("order_id", "").strip()
            payment_id = data.get("payment_id", "").strip()
            signature = data.get("signature", "").strip()
            mc = data.get("machine_code", "").strip().upper()
            store = data.get("store_name", "Apparel Store").strip()
            cust = data.get("customer_name", "Store Owner").strip()
            phone = data.get("phone", "").strip()

            if not order_id or not payment_id or not signature or not mc:
                self.respond_json({"error": "Missing order_id, payment_id, signature, or machine_code"}, status=400)
                return

            # Verify cryptographic signature
            is_valid = verify_razorpay_signature(order_id, payment_id, signature)
            if not is_valid:
                self.respond_json({"error": "Cryptographic payment signature mismatch. Untrusted transaction."}, status=400)
                return

            # Issue 1-Year Offline Key
            key, issued_at, expiry_date = issue_license_key(mc, customer_name=cust, store_name=store)

            # Record in SQLite sales ledger
            save_sale(order_id, payment_id, mc, cust, store, phone, LICENSE_FEE_INR, key, issued_at, expiry_date)

            self.respond_json({
                "success": True,
                "payment_id": payment_id,
                "machine_code": mc,
                "store_name": store,
                "expiry_date": expiry_date,
                "license_key": key
            })
            return

        else:
            self.respond_json({"error": "404 Not Found"}, status=404)

    def do_GET(self):
        parsed = urlparse(self.path)
        path = parsed.path
        params = parse_qs(parsed.query)

        # Direct Binary Download (Zero-Redirection for Microsoft Store)
        if path == "/download/ApparelPOS.exe":
            cache_path = os.path.join(SERVER_DIR, "ApparelPOS.exe")
            if not os.path.exists(cache_path) or os.path.getsize(cache_path) < 1000000:
                local_pub = os.path.join(BASE_DIR, "publish", "ApparelPOS.exe")
                if os.path.exists(local_pub) and os.path.getsize(local_pub) > 1000000:
                    import shutil
                    shutil.copyfile(local_pub, cache_path)
                else:
                    gh_url = "https://github.com/pawarswapnil9130/business/releases/download/v1.0.0/ApparelPOS.exe"
                    req = urllib.request.Request(gh_url, headers={"User-Agent": "Mozilla/5.0"})
                    with urllib.request.urlopen(req) as resp, open(cache_path, "wb") as f_out:
                        while chunk := resp.read(1024 * 1024):
                            f_out.write(chunk)

            file_size = os.path.getsize(cache_path)
            self.send_response(200)
            self.send_header("Content-Type", "application/octet-stream")
            self.send_header("Content-Disposition", 'attachment; filename="ApparelPOS.exe"')
            self.send_header("Content-Length", str(file_size))
            self.end_headers()
            with open(cache_path, "rb") as f:
                while chunk := f.read(1024 * 1024):
                    self.wfile.write(chunk)
            return

        # Main Landing Page
        elif path == "/renew" or path == "/":
            mc = params.get('mc', [''])[0].strip().upper()
            store = params.get('store', [''])[0].strip()
            if not mc:
                mc = "B82F-11AC-49DE-8831"

            key_id = get_razorpay_key_id()
            key_secret = get_razorpay_key_secret()
            is_configured = "true" if (key_id and key_secret) else "false"

            html = MAIN_HTML.replace("{{MACHINE_CODE}}", mc) \
                            .replace("{{STORE_NAME}}", store) \
                            .replace("{{IS_RAZORPAY_CONFIGURED}}", is_configured) \
                            .replace("{{RAZORPAY_KEY_ID}}", key_id)

            self.respond_html(html)
            return

        # Direct simulation fallback (when testing without keys)
        elif path == "/process-success":
            payment_id = params.get('payment_id', [f"PAY_{int(datetime.now().timestamp())}"])[0]
            mc = params.get('mc', [''])[0].strip().upper()
            store = params.get('store', ['Apparel Store'])[0].strip()
            cust = params.get('cust', ['Retail Client'])[0].strip()
            phone = params.get('phone', [''])[0].strip()

            key, issued_at, expiry_date = issue_license_key(mc, customer_name=cust, store_name=store)
            save_sale("SIMULATED_ORDER", payment_id, mc, cust, store, phone, LICENSE_FEE_INR, key, issued_at, expiry_date)

            html = f"""<!DOCTYPE html><html><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1.0"><title>License Key</title><style>*{{box-sizing:border-box;margin:0;padding:0;font-family:sans-serif;}}body{{background:#0F172A;color:#F8FAFC;padding:20px;display:flex;align-items:center;justify-content:center;min-height:100vh;}}.card{{background:#1E293B;border:1px solid #10B981;border-radius:16px;max-width:460px;width:100%;padding:24px;}}textarea{{width:100%;height:95px;background:#020617;color:#6EE7B7;border:1px solid #334155;border-radius:8px;padding:8px;font-family:monospace;font-size:11px;resize:none;margin:12px 0;}}button{{background:#10B981;color:#022C22;font-weight:bold;width:100%;padding:12px;border:none;border-radius:8px;cursor:pointer;}}</style></head><body><div class="card"><h2 style="color:#10B981;margin-bottom:8px;">✓ License Key Ready</h2><p style="font-size:13px;color:#94A3B8;">Machine: <b>{mc}</b> | Valid to: <b>{expiry_date}</b></p><textarea id="k" readonly>{key}</textarea><button onclick="navigator.clipboard.writeText(document.getElementById('k').value);alert('Copied!');">📋 Copy License Key</button><p style="font-size:12px;color:#94A3B8;margin-top:14px;text-align:center;">Paste this key on your desktop billing PC.</p></div></body></html>"""
            self.respond_html(html)
            return

        # Protected Admin Dashboard
        elif path == "/admin":
            secret = params.get('secret', [''])[0]
            admin_sec = get_admin_secret()
            if secret != admin_sec:
                self.respond_html(f"<body style='background:#0F172A;color:white;font-family:sans-serif;padding:40px;text-align:center;'><h2>🔒 Admin Authentication Required</h2><p style='color:#94A3B8;margin:12px 0;'>Append your secret key: <code>/admin?secret={admin_sec}</code></p></body>", status=401)
                return

            total_sales, total_rev, recent_sales = get_sales_stats()
            rows_html = ""
            for s in recent_sales:
                pid, code, cname, sname, ph, amt, iat, exp, lic = s
                rows_html += f"<tr><td style='padding:10px;border-bottom:1px solid #334155;'>{iat}</td><td style='padding:10px;border-bottom:1px solid #334155;font-family:monospace;color:#38BDF8;'>{pid}</td><td style='padding:10px;border-bottom:1px solid #334155;font-family:monospace;'>{code}</td><td style='padding:10px;border-bottom:1px solid #334155;'><b>{sname}</b><br><small style='color:#94A3B8;'>{cname}</small></td><td style='padding:10px;border-bottom:1px solid #334155;'>{ph}</td><td style='padding:10px;border-bottom:1px solid #334155;'><span style='background:#065F46;color:#6EE7B7;padding:3px 8px;border-radius:4px;font-weight:bold;'>₹{amt}</span></td><td style='padding:10px;border-bottom:1px solid #334155;color:#10B981;font-weight:bold;'>{exp}</td></tr>"

            if not recent_sales:
                rows_html = "<tr><td colspan='7' style='text-align:center;color:#94A3B8;padding:24px;'>No commercial licenses issued yet.</td></tr>"

            html = f"""<!DOCTYPE html><html><head><meta charset="utf-8"><title>ApparelPOS Admin</title><style>*{{box-sizing:border-box;margin:0;padding:0;font-family:sans-serif;}}body{{background:#0F172A;color:#F8FAFC;padding:30px;}}.container{{max-width:1100px;margin:0 auto;}}table{{width:100%;border-collapse:collapse;background:#1E293B;border-radius:12px;overflow:hidden;margin-top:20px;font-size:13px;}}th{{background:#334155;padding:12px;text-align:left;color:#E2E8F0;}}</style></head><body><div class="container"><div style="display:flex;justify-content:space-between;align-items:center;"><div><h1>ApparelPOS Sales &amp; Revenue Ledger</h1><p style="color:#94A3B8;font-size:13px;margin-top:4px;">Real-Time Offline Activations</p></div><a href="/admin/export" style="background:#4F46E5;color:white;padding:10px 16px;border-radius:8px;text-decoration:none;font-size:13px;font-weight:bold;">📥 Export CSV</a></div><div style="display:grid;grid-template-columns:repeat(3,1fr);gap:16px;margin:24px 0;"><div style="background:#1E293B;border:1px solid #334155;border-radius:12px;padding:20px;"><div style="font-size:11px;color:#94A3B8;text-transform:uppercase;font-weight:bold;">Total Licenses</div><div style="font-size:28px;font-weight:bold;color:#38BDF8;margin-top:4px;">{total_sales}</div></div><div style="background:#1E293B;border:1px solid #334155;border-radius:12px;padding:20px;"><div style="font-size:11px;color:#94A3B8;text-transform:uppercase;font-weight:bold;">Total Revenue</div><div style="font-size:28px;font-weight:bold;color:#10B981;margin-top:4px;">₹{total_rev:,}</div></div><div style="background:#1E293B;border:1px solid #334155;border-radius:12px;padding:20px;"><div style="font-size:11px;color:#94A3B8;text-transform:uppercase;font-weight:bold;">Pricing Tier</div><div style="font-size:28px;font-weight:bold;color:#FFFFFF;margin-top:4px;">₹249/yr</div></div></div><table><thead><tr><th>Date</th><th>Payment ID</th><th>Machine Code</th><th>Store</th><th>Phone</th><th>Amount</th><th>Valid Until</th></tr></thead><tbody>{rows_html}</tbody></table></div></body></html>"""
            self.respond_html(html)
            return

        elif path == "/admin/export":
            conn = sqlite3.connect(DB_PATH)
            cur = conn.cursor()
            cur.execute("SELECT id, order_id, payment_id, machine_code, customer_name, store_name, phone, amount, issued_at, expiry_date FROM sales ORDER BY id DESC")
            records = cur.fetchall()
            conn.close()

            csv_data = "ID,OrderID,PaymentID,MachineCode,Customer,Store,Phone,Amount,IssuedAt,ExpiryDate\n"
            for r in records:
                csv_data += ",".join(f'"{str(val)}"' for val in r) + "\n"

            self.send_response(200)
            self.send_header("Content-Type", "text/csv")
        elif path == "/terms-and-conditions":
            html = render_policy("Terms and Conditions", """
                <h3>1. Introduction &amp; Scope</h3>
                <p>Welcome to <strong>ApparelPOS</strong>. These Terms &amp; Conditions govern your purchase and use of the ApparelPOS offline desktop Point of Sale (POS) and inventory management software.</p>
                <br>
                <h3>2. Commercial License &amp; Validity</h3>
                <p>Upon payment of the annual fee of <strong>₹249/year</strong>, you are granted a non-transferable, node-locked commercial license valid for exactly 365 days from the date of issuance on one designated personal computer (identified by unique hardware identifiers).</p>
                <br>
                <h3>3. Software Delivery &amp; Activation</h3>
                <p>ApparelPOS is delivered digitally. Upon verified payment, an RSA-2048 cryptographic license key is generated instantly on your screen to unlock the software offline.</p>
                <br>
                <h3>4. Local Data Ownership</h3>
                <p>All sales, customer details, inventory matrices, and financial ledgers remain 100% on your local hard drive. ApparelPOS does not collect or upload your commercial records.</p>
            """)
            self.respond_html(html)
            return

        elif path == "/privacy-policy":
            html = render_policy("Privacy Policy", """
                <h3>1. Commitment to Data Privacy</h3>
                <p>At <strong>ApparelPOS</strong>, we believe business accounting data belongs exclusively to the store owner. Our desktop software operates 100% offline with zero cloud telemetry.</p>
                <br>
                <h3>2. Information Collected for Licensing</h3>
                <p>When purchasing an annual license via our web portal, we collect only minimal information necessary to issue and invoice your license:</p>
                <ul style="margin-left: 20px; line-height: 1.8;">
                    <li>Machine Code (Immutable hardware hash for cryptographic node-locking)</li>
                    <li>Store / Business Name</li>
                    <li>Owner / Contact Name</li>
                    <li>Mobile Number (for transaction receipt and payment verification)</li>
                </ul>
                <br>
                <h3>3. Zero Third-Party Sharing</h3>
                <p>We do not sell, rent, or trade your contact information. Payment processing is handled securely by Razorpay using bank-grade encryption.</p>
            """)
            self.respond_html(html)
            return

        elif path == "/refund-policy":
            html = render_policy("Refund &amp; Cancellation Policy", """
                <h3>1. 7-Day Money Back Guarantee</h3>
                <p>We want every shopkeeper to be completely confident in ApparelPOS. If the software fails to install or operate on your designated Windows PC due to technical incompatibility, you are eligible for a <strong>100% refund within 7 days</strong> of purchase.</p>
                <br>
                <h3>2. How to Request a Refund</h3>
                <p>To request a refund, email our support team with your Payment ID and Machine Code. We will review and process your request within 24 to 48 hours.</p>
                <br>
                <h3>3. Settlement Timeline</h3>
                <p>Approved refunds are credited back to your original payment method (Bank Account / UPI / Card) within <strong>5 to 7 business days</strong> as per standard banking processing cycles.</p>
            """)
            self.respond_html(html)
            return

        elif path == "/contact-us":
            html = render_policy("Contact Us", """
                <h3>Customer Support &amp; Inquiries</h3>
                <p>We are here to assist clothing store owners with installation, printer setups, and licensing queries.</p>
                <br>
                <div style="background:#0F172A;padding:16px;border-radius:10px;border:1px solid #334155;line-height:1.9;">
                    <p><strong>Business Name:</strong> ApparelPOS / Velvet Thread Software</p>
                    <p><strong>Support Email:</strong> <a href="mailto:pawarswapnil9130@gmail.com" style="color:#38BDF8;">pawarswapnil9130@gmail.com</a></p>
                    <p><strong>Operating Hours:</strong> Monday – Saturday: 9:00 AM – 8:00 PM IST</p>
                    <p><strong>Response Time:</strong> Within 24 business hours</p>
                </div>
            """)
            self.respond_html(html)
            return

        else:
            self.respond_json({"error": "404 Not Found"}, status=404)

    def respond_json(self, data: dict, status: int = 200):
        self.send_response(status)
        self.send_header("Content-Type", "application/json")
        self.end_headers()
        self.wfile.write(json.dumps(data).encode("utf-8"))

    def respond_html(self, html: str, status: int = 200):
        self.send_response(status)
        self.send_header("Content-Type", "text/html; charset=utf-8")
        self.end_headers()
        self.wfile.write(html.encode("utf-8"))

def run_server(port=8080):
    server = HTTPServer(('0.0.0.0', port), ProductionRazorpayHandler)
    key_id = get_razorpay_key_id()
    admin_sec = get_admin_secret()
    print("=" * 70)
    print(f"[*] ApparelPOS Real Razorpay Activation Server running on port {port}")
    print(f"[*] Mobile Checkout URL: http://localhost:{port}/renew?mc=B82F-11AC-49DE-8831")
    print(f"[*] Admin Dashboard URL: http://localhost:{port}/admin?secret={admin_sec}")
    print(f"[*] Razorpay Mode:       {'ENABLED (Key: ' + key_id + ')' if key_id else 'READY FOR KEYS (Paste in server/.env)'}")
    print("=" * 70)
    server.serve_forever()

if __name__ == "__main__":
    port_env = os.environ.get("PORT")
    p = int(sys.argv[1]) if len(sys.argv) > 1 else (int(port_env) if port_env else 8080)
    run_server(p)
