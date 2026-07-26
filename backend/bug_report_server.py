from flask import Flask, request, jsonify
from flask_cors import CORS
import smtplib
from email.mime.text import MIMEText
from email.mime.multipart import MIMEMultipart
import os

app = Flask(__name__)
CORS(app)

SMTP_SERVER = os.environ.get('SMTP_SERVER', 'smtp.gmail.com')
SMTP_PORT = int(os.environ.get('SMTP_PORT', 587))
SMTP_USER = os.environ.get('SMTP_USER', '')
SMTP_PASSWORD = os.environ.get('SMTP_PASSWORD', '')
TO_EMAIL = os.environ.get('TO_EMAIL', 'bugreports@tacticalfive.com')

@app.route('/api/bug-report', methods=['POST'])
def bug_report():
    data = request.get_json(force=True)
    body = data.get('body', '').strip()
    if not body:
        return jsonify({'success': False, 'error': 'Empty body'}), 400

    try:
        msg = MIMEMultipart()
        msg['Subject'] = 'Bug Report - Tactical Five'
        msg['From'] = SMTP_USER or 'noreply@tacticalfive.com'
        msg['To'] = TO_EMAIL
        msg.attach(MIMEText(body, 'plain', 'utf-8'))

        with smtplib.SMTP(SMTP_SERVER, SMTP_PORT) as server:
            server.starttls()
            if SMTP_USER and SMTP_PASSWORD:
                server.login(SMTP_USER, SMTP_PASSWORD)
            server.send_message(msg)

        return jsonify({'success': True})
    except Exception as e:
        return jsonify({'success': False, 'error': str(e)}), 500

@app.route('/api/bug-report', methods=['OPTIONS'])
def bug_report_options():
    return '', 200

if __name__ == '__main__':
    app.run(host='0.0.0.0', port=int(os.environ.get('PORT', 5000)))
