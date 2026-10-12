import os
import sys

try:
    from fpdf import FPDF
except ImportError:
    print("fpdf library not found. Install it via: pip install fpdf2")
    sys.exit(1)

class PDF(FPDF):
    def header(self):
        self.set_font('Helvetica', 'B', 12)
        self.cell(0, 8, 'GTA V Dynamic World Engine - Complete Source Code Audit', 0, 1, 'C')
        self.ln(2)

    def footer(self):
        self.set_y(-15)
        self.set_font('Helvetica', 'I', 8)
        self.cell(0, 10, f'Page {self.page_no()}', 0, 0, 'C')

def generate_audit():
    pdf = PDF()
    pdf.set_auto_page_break(auto=True, margin=15)
    pdf.add_page()

    pdf.set_font('Helvetica', 'B', 16)
    pdf.cell(0, 10, 'Dynamic World Engine (SHVDN v3 / AI System)', 0, 1, 'L')
    pdf.set_font('Helvetica', 'I', 10)
    pdf.cell(0, 6, 'Milestones 1, 2, and 3 - Repository Source Audit', 0, 1, 'L')
    pdf.ln(5)

    base_dir = os.path.dirname(os.path.abspath(__file__))
    source_dirs = ['src', 'tests', 'JevDecisionLab']

    file_list = []
    for s_dir in source_dirs:
        full_path = os.path.join(base_dir, s_dir)
        if os.path.exists(full_path):
            for root, _, files in os.walk(full_path):
                for f in files:
                    if f.endswith('.cs') or f.endswith('.json') or f.endswith('.js') or f.endswith('.md'):
                        file_list.append(os.path.join(root, f))

    for filepath in sorted(file_list):
        rel_path = os.path.relpath(filepath, base_dir)
        pdf.set_font('Helvetica', 'B', 12)
        pdf.set_fill_color(230, 230, 230)
        pdf.cell(0, 8, f'FILE: {rel_path}', 0, 1, 'L', fill=True)
        pdf.ln(2)

        try:
            with open(filepath, 'r', encoding='utf-8', errors='ignore') as f:
                content = f.read()

            pdf.set_font('Courier', '', 7)
            for line in content.split('\n'):
                # Clean characters for latin-1 fpdf compatibility
                safe_line = line.encode('latin-1', 'replace').decode('latin-1')
                pdf.multi_cell(0, 3.5, safe_line)
            pdf.ln(4)
        except Exception as e:
            pdf.set_font('Helvetica', 'I', 8)
            pdf.cell(0, 5, f'Error reading file: {e}', 0, 1, 'L')
            pdf.ln(2)

    output_path = os.path.join(base_dir, 'repository_audit.pdf')
    pdf.output(output_path)
    print(f"[SUCCESS] Repository audit PDF generated successfully at: {output_path}")

if __name__ == '__main__':
    generate_audit()
