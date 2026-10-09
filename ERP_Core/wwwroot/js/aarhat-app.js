/* ==========================================================================
   ZAHID HUSSAIN & SONS — FISH AARHAT SYSTEM APP JS
   Interactive Calculations, Dynamic Rows, Tree Drill-Down & Print Previews
   ========================================================================== */

// Global state for calculations and preview
window.AarhatApp = (function () {

    // Number formatting helper (Pakistani/Standard Currency)
    function fmtRs(num) {
        if (num === null || num === undefined || isNaN(num)) return "Rs 0";
        return "Rs " + Math.round(num).toLocaleString('en-PK');
    }

    // Toggle Form visibility
    function toggleForm(formId) {
        const el = document.getElementById(formId);
        if (!el) return;
        el.style.display = (el.style.display === 'none' || el.style.display === '') ? 'block' : 'none';
        if (el.style.display === 'block') {
            el.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
        }
    }

    // Mobile Sidebar Drawer Toggle
    function toggleSidebar() {
        const sidebar = document.getElementById('sidebarEl');
        const overlay = document.getElementById('sidebarOverlay');
        if (sidebar) sidebar.classList.toggle('open');
        if (overlay) overlay.classList.toggle('show');
    }

    // --------------------------------------------------------------------------
    // 3-BOX BILL CALCULATION ENGINE
    // --------------------------------------------------------------------------
    function recalcBill() {
        let billTotal = 0;
        const rows = document.querySelectorAll('#billLotBody tr');

        rows.forEach(row => {
            const weightInput = row.querySelector('.lot-weight');
            const rateInput = row.querySelector('.lot-rate');
            const amtCell = row.querySelector('.lot-amount');

            const weight = parseFloat(weightInput?.value) || 0;
            const rate = parseFloat(rateInput?.value) || 0;
            const rowAmt = weight * rate;

            if (amtCell) {
                amtCell.textContent = fmtRs(rowAmt);
            }
            billTotal += rowAmt;
        });

        // Update Total row
        const billTotalCell = document.getElementById('billTotalCell');
        if (billTotalCell) billTotalCell.textContent = fmtRs(billTotal);

        // Box 1: Commission
        const commBase = document.getElementById('commBase');
        if (commBase) commBase.textContent = fmtRs(billTotal);

        const commPctInput = document.getElementById('commPct');
        const commPct = parseFloat(commPctInput?.value) || 0;
        const commAmt = (billTotal * commPct) / 100;
        const commAmtEl = document.getElementById('commAmt');
        if (commAmtEl) commAmtEl.textContent = fmtRs(commAmt);

        // Box 2: Expenses
        const expLabour = parseFloat(document.getElementById('expLabour')?.value) || 0;
        const expLoading = parseFloat(document.getElementById('expLoading')?.value) || 0;
        const expOther = parseFloat(document.getElementById('expOther')?.value) || 0;
        const totalExp = expLabour + expLoading + expOther;
        const expTotalEl = document.getElementById('expTotal');
        if (expTotalEl) expTotalEl.textContent = fmtRs(totalExp);

        // Box 3: Settlement
        const settleTotal = document.getElementById('settleTotal');
        if (settleTotal) settleTotal.textContent = fmtRs(billTotal);

        const settleComm = document.getElementById('settleComm');
        if (settleComm) settleComm.textContent = "- " + fmtRs(commAmt);

        const settleExp = document.getElementById('settleExp');
        if (settleExp) settleExp.textContent = "- " + fmtRs(totalExp);

        const netPayable = Math.max(0, billTotal - commAmt - totalExp);
        const settleNet = document.getElementById('settleNet');
        if (settleNet) settleNet.textContent = fmtRs(netPayable);
    }

    let lotRowCounter = 1;

    function addBillLotRow(lotNo = '', fish = '', supplier = '', weight = 0, rate = 0) {
        const tbody = document.getElementById('billLotBody');
        if (!tbody) return;

        lotRowCounter++;
        const rowId = 'billRow_' + lotRowCounter;
        const tr = document.createElement('tr');
        tr.id = rowId;

        tr.innerHTML = `
            <td style="color:var(--text-faint);">${tbody.children.length + 1}</td>
            <td><input type="text" class="lot-no" placeholder="LOT-${3400 + tbody.children.length}" value="${lotNo}" /></td>
            <td>
                <select class="lot-fish">
                    <option ${fish === 'Pomfret' ? 'selected' : ''}>Pomfret</option>
                    <option ${fish === 'Rohu' ? 'selected' : ''}>Rohu</option>
                    <option ${fish === 'Sole Fish' ? 'selected' : ''}>Sole Fish</option>
                    <option ${fish === 'Shrimp (Jumbo)' ? 'selected' : ''}>Shrimp (Jumbo)</option>
                    <option ${fish === 'Palla' ? 'selected' : ''}>Palla</option>
                </select>
            </td>
            <td>
                <select class="lot-supplier">
                    <option ${supplier.includes('Younus') ? 'selected' : ''}>Younus Boat Co.</option>
                    <option ${supplier.includes('Rasool') ? 'selected' : ''}>Rasool Bux Mallah</option>
                    <option ${supplier.includes('Karim') ? 'selected' : ''}>Haji Karim Baksh</option>
                    <option ${supplier.includes('Sikandar') ? 'selected' : ''}>Sikandar Fisheries</option>
                </select>
            </td>
            <td><input type="number" class="lot-weight tnum" value="${weight || ''}" placeholder="0" oninput="AarhatApp.recalcBill()" /></td>
            <td><input type="number" class="lot-rate tnum" value="${rate || ''}" placeholder="0" oninput="AarhatApp.recalcBill()" /></td>
            <td class="lot-amount tnum" style="font-weight:600;">Rs 0</td>
            <td style="text-align:center;">
                <button type="button" class="lot-remove" onclick="AarhatApp.removeBillLotRow('${rowId}')" title="Remove lot">
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M18 6L6 18M6 6l12 12"/></svg>
                </button>
            </td>
        `;

        tbody.appendChild(tr);
        recalcBill();
    }

    function removeBillLotRow(rowId) {
        const row = document.getElementById(rowId);
        if (row) {
            row.remove();
            // Re-index remaining rows
            const rows = document.querySelectorAll('#billLotBody tr');
            rows.forEach((r, idx) => {
                if (r.cells[0]) r.cells[0].textContent = idx + 1;
            });
            recalcBill();
        }
    }

    // --------------------------------------------------------------------------
    // SUBTAB SWITCHING (Entry View vs Monthly Report)
    // --------------------------------------------------------------------------
    function showSubTab(prefix, tab) {
        const entryView = document.getElementById(prefix + 'EntryView');
        const reportView = document.getElementById(prefix + 'ReportView');
        const entryBtn = document.getElementById(prefix + 'TabEntryBtn');
        const reportBtn = document.getElementById(prefix + 'TabReportBtn');

        if (tab === 'entry') {
            if (entryView) entryView.style.display = 'block';
            if (reportView) reportView.style.display = 'none';
            if (entryBtn) entryBtn.classList.add('active');
            if (reportBtn) reportBtn.classList.remove('active');
        } else {
            if (entryView) entryView.style.display = 'none';
            if (reportView) reportView.style.display = 'block';
            if (entryBtn) entryBtn.classList.remove('active');
            if (reportBtn) reportBtn.classList.add('active');
        }
    }

    // --------------------------------------------------------------------------
    // UNIVERSAL PRINT PREVIEW MODAL ENGINE
    // --------------------------------------------------------------------------
    function openPrintModal(title, htmlContent) {
        const modal = document.getElementById('printModal');
        const tag = document.getElementById('printModalTag');
        const body = document.getElementById('printModalBody');

        if (tag) tag.textContent = title || "Print Document";
        if (body) body.innerHTML = htmlContent;
        if (modal) modal.classList.add('show');
        document.body.style.overflow = 'hidden';
    }

    function closePrint() {
        const modal = document.getElementById('printModal');
        if (modal) modal.classList.remove('show');
        document.body.style.overflow = '';
    }

    // Maund-Kg-Pao Formatter Helper
    function formatKgToMaund(weightKg) {
        const w = parseFloat(weightKg) || 0;
        if (w <= 0) return "000-00-0";
        const maund = Math.floor(w / 40);
        const rem = w - (maund * 40);
        const kg = Math.floor(rem);
        const paoRem = rem - kg;
        let pao = 0;
        if (paoRem >= 0.70) pao = 3;
        else if (paoRem >= 0.45) pao = 2;
        else if (paoRem >= 0.20) pao = 1;
        return `${String(maund).padStart(3, '0')}-${String(kg).padStart(2, '0')}-${pao}`;
    }

    function doPrint() {
        document.body.classList.add('printing');
        const prevTitle = document.title;
        document.title = "";
        window.print();
        setTimeout(() => {
            document.title = prevTitle;
            document.body.classList.remove('printing');
        }, 800);
    }

    // Generic Print Helpers for Documents
    function printBillInvoice(billNo, supplier, date, totalSale, commAmt, expAmt, netPayable, lots, deposit, status, paidToSupplier, labourExp, iceExp, freightExp, otherExp, farmerIceExp) {
        const depositAmt = parseFloat(deposit) || 0;
        const totalLots = (lots || []).length;

        // Total Weight calculation in Maund-Kg-Pao format
        const totalWeightKg = (lots || []).reduce((sum, l) => sum + (parseFloat(l.weight) || 0), 0);
        const totalWeightMaundStr = formatKgToMaund(totalWeightKg);

        // Average Rate calculation = Total Gross Sale divided by Total Converted Weight in Kg (hidden in report)
        const avgRate = totalWeightKg > 0 ? (totalSale / totalWeightKg) : 0;
        const avgRateMaund = avgRate * 40;

        // Expenses separation: Labour, Ice, Farmer Ice, Freight, Other
        const labourAmt = (labourExp !== undefined && labourExp !== null) ? (parseFloat(labourExp) || 0) : 0;
        const iceAmt = (iceExp !== undefined && iceExp !== null) ? (parseFloat(iceExp) || 0) : 0;
        const farmerIceAmt = (farmerIceExp !== undefined && farmerIceExp !== null) ? (parseFloat(farmerIceExp) || 0) : 0;
        const freightAmt = (freightExp !== undefined && freightExp !== null) ? (parseFloat(freightExp) || 0) : 0;
        const otherAmt = (otherExp !== undefined && otherExp !== null) ? (parseFloat(otherExp) || 0) : 0;
        const hasSeparatedExpenses = (labourAmt > 0 || iceAmt > 0 || farmerIceAmt > 0 || freightAmt > 0 || otherAmt > 0 || (labourExp !== undefined && iceExp !== undefined));
        const combinedExpAmt = parseFloat(expAmt) || (labourAmt + iceAmt + farmerIceAmt + freightAmt + otherAmt);

        const lotsHtml = (lots || []).map((l, i) => `
            <tr>
                <td style="text-align:center;font-size:12px;color:#555;">${i + 1}</td>
                <td style="text-align:center;font-family:monospace;font-weight:700;font-size:12.5px;">${l.lotNo || 'LOT'}</td>
                <td class="col-desc" style="word-break:break-word;white-space:normal;font-size:12.5px;font-weight:600;color:#111;">${l.fish || 'Fish'}</td>
                <td style="text-align:right;white-space:nowrap;">
                    <div style="font-family:monospace;font-weight:800;font-size:14.5px;color:#0f5132;line-height:1.2;">
                        ${l.weightStr || formatKgToMaund(l.weight)}
                    </div>
                    <span class="report-lot-converted-weight" style="display:none;" data-weight-kg="${Number(parseFloat(l.weight) || 0)}"></span>
                </td>
                <td style="text-align:right;white-space:nowrap;font-size:13px;font-weight:600;">Rs ${(parseFloat(l.rate) || 0).toLocaleString()}</td>
                <td style="text-align:right;font-weight:700;white-space:nowrap;font-size:13.5px;color:#111;">Rs ${Math.round((parseFloat(l.weight) || 0) * (parseFloat(l.rate) || 0)).toLocaleString()}</td>
            </tr>
        `).join('');

        let statusBadge = '';
        const st = (status || 'Due').toUpperCase();
        if (st === 'PAID') {
            statusBadge = `<span style="display:inline-block;padding:3px 10px;background:#e8f5e9;color:#2e7d32;border:1px solid #a5d6a7;border-radius:4px;font-weight:700;font-size:11.5px;letter-spacing:0.5px;">PAID / ادا شدہ</span>`;
        } else if (st === 'PARTIAL') {
            const paidTxt = (paidToSupplier && paidToSupplier > 0) ? ` · Rs ${Math.round(paidToSupplier).toLocaleString()}` : '';
            statusBadge = `<span style="display:inline-block;padding:3px 10px;background:#fff8e1;color:#b78103;border:1px solid #ffe082;border-radius:4px;font-weight:700;font-size:11.5px;letter-spacing:0.5px;">PARTIAL${paidTxt} / جزوی</span>`;
        } else {
            statusBadge = `<span style="display:inline-block;padding:3px 10px;background:#ffebee;color:#c62828;border:1px solid #ffcdd2;border-radius:4px;font-weight:700;font-size:11.5px;letter-spacing:0.5px;">DUE / PENDING / واجب الادا</span>`;
        }

        const content = `
            <div class="pp-head">
                <div class="pp-brand" style="flex:1;">
                    <div style="font-size:25px;font-weight:900;letter-spacing:0.5px;line-height:1.15;color:#111;">
                        ZAHID HUSSAIN <span style="color:#8a5a1e;">&amp; SONS</span>
                    </div>
                    <div class="sub" style="font-size:12px;color:#444;font-weight:600;margin-top:3px;">Fish Commission Agents · Sukkur Fish Harbour</div>
                    <div class="sub" style="font-size:11.5px;color:#555;font-weight:600;margin-top:2px;white-space:nowrap;">Zahid Hussain (0300-3110890) &nbsp;|&nbsp; Yaseen (0334-3942837)</div>
                </div>
                <div class="pp-doc-title">
                    <h2>FISH AUCTION BILL (PARCHI)</h2>
                    <div class="sub">Bill #: <strong>${billNo || 'BL-1215'}</strong></div>
                </div>
            </div>

            <div class="pp-meta" style="margin-bottom:14px;">
                <div><b>Supplier (Boat / Farmer):</b> <strong>${supplier || 'General Boat Catch'}</strong></div>
                <div><b>Date:</b> ${date || new Date().toLocaleDateString('en-GB')}</div>
                <div><b>Payment Status:</b> ${statusBadge}</div>
            </div>

            <table class="pp-table" style="width:100%;table-layout:fixed;border-collapse:collapse;">
                <thead>
                    <tr>
                        <th style="width:32px;text-align:center;">#</th>
                        <th style="width:82px;text-align:center;">Lot #</th>
                        <th style="width:24%;text-align:left;">Fish Description</th>
                        <th style="width:25%;text-align:right;">Weight (Md-Kg-P)</th>
                        <th style="width:15%;text-align:right;">Rate / Kg</th>
                        <th style="width:20%;text-align:right;">Total (Rs)</th>
                    </tr>
                </thead>
                <tbody>
                    ${lotsHtml || `<tr><td colspan="6" style="text-align:center;padding:12px;">No lot records</td></tr>`}
                </tbody>
                <tfoot>
                    <tr style="background:#f4f3ed;border-top:2px solid #222;">
                        <td colspan="3" style="text-align:right;font-weight:700;font-size:12.5px;padding:8px 6px;">Total Weight &amp; Gross Total:</td>
                        <td style="text-align:right;white-space:nowrap;padding:8px 6px;">
                            <div style="font-family:monospace;font-weight:800;font-size:14.5px;color:#0f5132;line-height:1.2;">
                                ${totalWeightMaundStr}
                            </div>
                            <span id="reportTotalConvertedWeight" style="display:none;" data-total-weight-kg="${totalWeightKg.toFixed(2)}"></span>
                        </td>
                        <td style="text-align:right;white-space:nowrap;padding:8px 6px;">
                            <div style="font-weight:700;font-size:12.5px;color:#8a5a1e;line-height:1.2;">Rs ${avgRate.toFixed(2)} <span style="font-size:10.5px;font-weight:600;color:#555;">/ Kg</span></div>
                            <div style="font-weight:800;font-size:12px;color:#0f5132;margin-top:2px;line-height:1.2;">Rs ${avgRateMaund.toLocaleString('en-US', { minimumFractionDigits: 1, maximumFractionDigits: 2 })} <span style="font-size:10px;font-weight:600;color:#555;">/ Md</span></div>
                            <div style="font-size:9.5px;color:#666;font-weight:normal;">(Avg Rate / اوسط ریٹ)</div>
                        </td>
                        <td style="text-align:right;font-weight:800;font-size:14px;color:#111;white-space:nowrap;padding:8px 6px;">
                            ${fmtRs(totalSale)}
                        </td>
                    </tr>
                </tfoot>
            </table>

            <div class="pp-summary-grid" style="display:flex;justify-content:space-between;align-items:flex-start;gap:20px;margin-top:14px;margin-bottom:18px;">
                <!-- Left Section: Expenses Detail (اخراجات کی تفصیل) -->
                <div class="pp-summary-left" style="flex:1;background:#fcfbf8;border:1px solid #e7dfd0;border-radius:6px;padding:10px 14px;">
                    <div style="font-size:12px;font-weight:700;color:#8a5a1e;border-bottom:1px solid #e7dfd0;padding-bottom:5px;margin-bottom:6px;display:flex;justify-content:space-between;align-items:center;">
                        <span>Expenses Breakdown (اخراجات تفصیل)</span>
                        <span style="font-size:11px;color:#777;font-weight:600;">Deductions</span>
                    </div>
                    ${hasSeparatedExpenses ? `
                        ${labourAmt > 0 ? `<div style="display:flex;justify-content:space-between;padding:4px 0;border-bottom:1px solid #f0ede6;font-size:12px;"><span>Labour Expense (مزدوری):</span> <span style="font-weight:600;">- ${fmtRs(labourAmt)}</span></div>` : ''}
                        ${iceAmt > 0 ? `<div style="display:flex;justify-content:space-between;padding:4px 0;border-bottom:1px solid #f0ede6;font-size:12px;"><span>Ice Expense (برف):</span> <span style="font-weight:600;">- ${fmtRs(iceAmt)}</span></div>` : ''}
                        ${farmerIceAmt > 0 ? `<div style="display:flex;justify-content:space-between;padding:4px 0;border-bottom:1px solid #f0ede6;font-size:12px;"><span>Farmer Ice (زمیندار برف):</span> <span style="font-weight:600;">- ${fmtRs(farmerIceAmt)}</span></div>` : ''}
                        ${freightAmt > 0 ? `<div style="display:flex;justify-content:space-between;padding:4px 0;border-bottom:1px solid #f0ede6;font-size:12px;"><span>Freight Expense (کرایہ):</span> <span style="font-weight:600;">- ${fmtRs(freightAmt)}</span></div>` : ''}
                        ${otherAmt > 0 ? `<div style="display:flex;justify-content:space-between;padding:4px 0;border-bottom:1px solid #f0ede6;font-size:12px;"><span>Other Expense (دیگر خرچ):</span> <span style="font-weight:600;">- ${fmtRs(otherAmt)}</span></div>` : ''}
                        ${combinedExpAmt <= 0 ? `<div style="padding:8px 0;text-align:center;color:#888;font-size:12px;font-style:italic;">No expenses charged (کوئی خرچہ نہیں)</div>` : ''}
                    ` : `
                        ${combinedExpAmt > 0 ? `<div style="display:flex;justify-content:space-between;padding:4px 0;border-bottom:1px solid #f0ede6;font-size:12px;"><span>Expenses (خرچہ):</span> <span style="font-weight:600;">- ${fmtRs(combinedExpAmt)}</span></div>` : `<div style="padding:8px 0;text-align:center;color:#888;font-size:12px;font-style:italic;">No expenses charged (کوئی خرچہ نہیں)</div>`}
                    `}
                    <div style="display:flex;justify-content:space-between;padding:6px 0;margin-top:3px;border-top:1px solid #dcd5c7;font-weight:700;font-size:12.5px;color:${combinedExpAmt > 0 ? '#c62828' : '#555'};">
                        <span>Total Expenses (کل اخراجات):</span>
                        <strong style="font-family:monospace;">${combinedExpAmt > 0 ? '- ' + fmtRs(combinedExpAmt) : 'Rs 0'}</strong>
                    </div>
                </div>

                <!-- Right Section: Settlement & Net Payable (حتمی کھاتہ / نیٹ ادائیگی) -->
                <div class="pp-summary-right pp-totals" style="flex:1;margin:0;max-width:380px;background:#fcfbf8;border:1px solid #e7dfd0;border-radius:6px;padding:10px 14px;">
                    <div style="font-size:12px;font-weight:700;color:#0f5132;border-bottom:1px solid #e7dfd0;padding-bottom:5px;margin-bottom:6px;display:flex;justify-content:space-between;align-items:center;">
                        <span>Settlement Summary (حتمی حساب)</span>
                        <span style="font-size:11px;color:#777;font-weight:600;">Amount (Rs)</span>
                    </div>
                    <div style="display:flex;justify-content:space-between;padding:4px 0;border-bottom:1px solid #f0ede6;font-size:12.5px;"><span>Gross Sale Amount:</span> <strong style="color:#111;">${fmtRs(totalSale)}</strong></div>
                    <div style="display:flex;justify-content:space-between;padding:4px 0;border-bottom:1px solid #f0ede6;font-size:12.5px;"><span>Commission Deducted:</span> <span style="color:#8a5a1e;font-weight:600;">- ${fmtRs(commAmt)}</span></div>
                    ${combinedExpAmt > 0 ? `<div style="display:flex;justify-content:space-between;padding:4px 0;border-bottom:1px solid #f0ede6;font-size:12.5px;"><span>Total Expenses:</span> <span style="color:#c62828;font-weight:600;">- ${fmtRs(combinedExpAmt)}</span></div>` : ''}
                    ${depositAmt > 0 ? `<div style="display:flex;justify-content:space-between;padding:4px 0;border-bottom:1px solid #f0ede6;font-size:12.5px;"><span>Deposit / Martey (مارتی):</span> <span style="color:#c62828;font-weight:600;">- ${fmtRs(depositAmt)}</span></div>` : ''}
                    <div class="final" style="display:flex;justify-content:space-between;align-items:center;padding:7px 0;font-size:14px;font-weight:800;border-top:2px solid #161616;border-bottom:2px solid #161616;margin-top:6px;">
                        <span style="white-space:nowrap;">Net Supplier Payable:</span>
                        <strong style="white-space:nowrap;font-size:15.5px;color:#0f5132;font-family:monospace;">${fmtRs(netPayable)}</strong>
                    </div>
                </div>
            </div>

            <div class="pp-sign" style="margin-top:40px;">
                <div>Munshi Signature</div>
                <div>Supplier / Boat Owner</div>
                <div>Proprietor Stamp</div>
            </div>

            <div class="pp-foot" style="margin-top:20px;">
                <div>This is an official transaction document generated by Zahid Hussain &amp; Sons Aarhat ERP.</div>
                <div style="margin-top:6px;font-size:10.5px;color:#666;border-top:1px dashed #dedede;padding-top:6px;display:flex;justify-content:space-between;align-items:center;flex-wrap:wrap;gap:4px 12px;">
                    <span>Software Design &amp; Develope: <strong style="color:#222;">ICT System PVT Ltd</strong></span>
                    <span>mob: <strong style="color:#222;">+92 3313223528</strong></span>
                    <span>Email: <strong style="color:#222;">sales@ictsystems.com.pk</strong></span>
                </div>
            </div>
        `;

        openPrintModal("Print Bill — " + billNo, content);
    }

    // Print current interactive bill
    function printCurrentBill() {
        const billNo = document.getElementById('billNoInput')?.value || 'BL-1215';
        const date = document.getElementById('billDateInput')?.value || new Date().toISOString().split('T')[0];
        const supplierSelect = document.getElementById('billSupplierSelect');
        const supplier = supplierSelect ? supplierSelect.options[supplierSelect.selectedIndex]?.text : 'Supplier';

        const lotRows = [];
        document.querySelectorAll('#billLotBody tr').forEach(r => {
            const lotNo = r.querySelector('.lot-no')?.value || 'LOT';
            const fish = r.querySelector('.lot-fish option:checked')?.text || 'Fish';
            const customer = r.querySelector('.lot-customer option:checked')?.text || 'Buyer';
            const weight = parseFloat(r.querySelector('.lot-weight')?.value) || 0;
            const rate = parseFloat(r.querySelector('.lot-rate')?.value) || 0;
            const weightStr = r.querySelector('.lot-weight-str')?.value || '';
            if (weight > 0 || rate > 0) {
                lotRows.push({ lotNo, fish, customer, weight, rate, weightStr });
            }
        });

        const totalSale = parseFloat(document.getElementById('grossTotalDisplay')?.textContent.replace(/[^0-9.]/g, '') || document.getElementById('billTotalCell')?.textContent.replace(/[^0-9.]/g, '')) || 0;
        const commAmt = parseFloat(document.getElementById('commAmtDisplay')?.textContent.replace(/[^0-9.]/g, '') || document.getElementById('commAmtInput')?.value) || 0;
        const expLabour = parseFloat(document.getElementById('expLabour')?.value) || 0;
        const expIce = parseFloat(document.getElementById('expIce')?.value) || 0;
        const expFarmerIce = parseFloat(document.getElementById('expFarmerIce')?.value) || 0;
        const expFreight = parseFloat(document.getElementById('expFreight')?.value) || 0;
        const expOther = parseFloat(document.getElementById('expOther')?.value) || 0;
        const expAmt = parseFloat(document.getElementById('totalExpDisplay')?.textContent.replace(/[^0-9.]/g, '')) || (expLabour + expIce + expFarmerIce + expFreight + expOther);
        const deposit = parseFloat(document.getElementById('billDepositInput')?.value || document.getElementById('expDeposit')?.value) || 0;
        const netPayable = Math.max(0, totalSale - commAmt - expAmt - deposit);
        const status = document.getElementById('billStatusSelect')?.value || 'Due';

        printBillInvoice(billNo, supplier, date, totalSale, commAmt, expAmt, netPayable, lotRows, deposit, status, 0, expLabour, expIce, expFreight, expOther, expFarmerIce);
    }

    // Print Individual Lot / Bill Item Report
    function printBillItemReport(billNo, date, supplier, lotNo, fish, customer, weightStr, weightKg, rate, amount, voucherNo, status, isPaid) {
        const netWeight = (parseFloat(weightKg) || 0).toFixed(2) + ' Kg';
        const rateFormatted = fmtRs(rate) + ' / Kg';
        const totalFormatted = fmtRs(amount);

        const content = `
            <div class="pp-head">
                <div class="pp-brand">
                    <div style="font-size:25px;font-weight:900;letter-spacing:0.5px;line-height:1.15;color:#111;">
                        ZAHID HUSSAIN <span style="color:#8a5a1e;">&amp; SONS</span>
                    </div>
                    <div class="sub" style="font-size:12px;color:#444;font-weight:600;margin-top:3px;">Fish Commission Agents · Sukkur Fish Harbour</div>
                    <div class="sub" style="font-size:11.5px;color:#555;font-weight:600;margin-top:2px;">Owner: Zahid Hussain (0300-3110890) | Munshi: Yaseen (0334-3942837)</div>
                </div>
                <div class="pp-doc-title">
                    <h2>LOT AUCTION SALE SLIP</h2>
                    <div style="font-size:12px;font-weight:700;color:#8a5a1e;margin-top:2px;">لاٹ پرچی (خریدار کھری رسید)</div>
                    <div class="sub" style="margin-top:4px;">Lot #: <strong>${lotNo || 'LOT'}</strong> · Bill #: <strong>${billNo || 'BL-0000'}</strong></div>
                </div>
            </div>

            <div class="pp-meta" style="grid-template-columns: 1.5fr 1.5fr 1fr;gap:12px;background:#fcfbf8;border:1px solid #e7dfd0;padding:12px 14px;border-radius:6px;align-items:center;">
                <div>
                    <b style="font-size:10px;color:#777;letter-spacing:.04em;text-transform:uppercase;margin-bottom:3px;display:block;">Customer (Buyer / خریدار)</b>
                    <strong style="color:#0f5132;font-size:13.5px;display:block;word-break:break-word;">${customer || 'General Buyer'}</strong>
                </div>
                <div>
                    <b style="font-size:10px;color:#777;letter-spacing:.04em;text-transform:uppercase;margin-bottom:3px;display:block;">Voucher Ref / واؤچر</b>
                    <span style="font-family:monospace;font-weight:600;font-size:12.5px;color:#8a5a1e;display:block;">${voucherNo || ('JV-' + (billNo || 'BILL'))}</span>
                </div>
                <div style="text-align:right;">
                    <b style="font-size:10px;color:#777;letter-spacing:.04em;text-transform:uppercase;margin-bottom:3px;display:block;">Date / تاریخ</b>
                    <span style="font-weight:600;font-size:12.5px;color:#111;">${date || new Date().toLocaleDateString('en-GB')}</span>
                </div>
            </div>

            <table class="pp-table" style="width:100%;table-layout:fixed;border-collapse:collapse;margin-top:12px;">
                <thead>
                    <tr>
                        <th style="width:14%;text-align:center;">Lot #<div style="font-size:9.5px;color:#777;font-weight:normal;margin-top:1px;">لاٹ نمبر</div></th>
                        <th style="width:34%;text-align:left;">Fish Description<div style="font-size:9.5px;color:#777;font-weight:normal;margin-top:1px;">جنس مچھلی</div></th>
                        <th style="width:22%;text-align:center;">Weight (Md-Kg-P)<div style="font-size:9.5px;color:#777;font-weight:normal;margin-top:1px;">من-کلو-پاؤ</div></th>
                        <th style="width:14%;text-align:right;">Rate / Kg<div style="font-size:9.5px;color:#777;font-weight:normal;margin-top:1px;">ریٹ فی کلو</div></th>
                        <th style="width:16%;text-align:right;">Amount (Rs)<div style="font-size:9.5px;color:#777;font-weight:normal;margin-top:1px;">کل رقم</div></th>
                    </tr>
                </thead>
                <tbody>
                    <tr>
                        <td style="text-align:center;font-family:monospace;font-weight:700;font-size:12.5px;vertical-align:middle;">${lotNo || 'LOT'}</td>
                        <td style="word-break:break-word;vertical-align:top;">
                            <div style="font-weight:700;font-size:13px;color:#111;">${fish || 'Fish'}</div>
                            <div style="font-size:10.5px;color:#666;margin-top:3px;">Buyer: <strong style="color:#0f5132;">${customer || 'General Buyer'}</strong></div>
                        </td>
                        <td style="text-align:center;font-family:monospace;font-weight:700;color:#8a5a1e;font-size:13px;vertical-align:middle;">
                            ${weightStr || '000-00-0'}
                        </td>
                        <td style="text-align:right;font-size:12.5px;vertical-align:middle;">
                            ${rateFormatted}
                        </td>
                        <td style="text-align:right;font-weight:700;font-size:13.5px;color:#111;vertical-align:middle;">
                            ${totalFormatted}
                        </td>
                    </tr>
                </tbody>
            </table>

            <div style="display:flex;justify-content:space-between;align-items:stretch;margin-top:14px;gap:16px;">
                <div style="flex:1;background:#f9f9f9;border:1px solid #e2e2e2;border-radius:6px;padding:12px 14px;font-size:11.5px;color:#444;display:flex;flex-direction:column;justify-content:center;gap:6px;">
                    <div><b>Auction Summary:</b> Verified sale of <strong>${weightStr || netWeight}</strong> of <strong>${fish}</strong> at <strong>${rateFormatted}</strong>.</div>
                    <div style="color:#666;">Bill Reference: <strong>${billNo}</strong> &bull; Ledger: <strong style="color:#0f5132;">${customer || 'General Buyer'}</strong>.</div>
                </div>

                <div class="pp-totals" style="margin:0;width:270px;background:#fcfbf8;border:1px solid #e7dfd0;border-radius:6px;padding:8px 14px;">
                    <div style="display:flex;justify-content:space-between;padding:5px 0;border-bottom:1px solid #e2e2e2;font-size:12px;"><span>Weight (Md-Kg-P):</span> <strong style="font-family:monospace;color:#0f5132;">${weightStr || '000-00-0'}</strong><span style="display:none;" data-weight-kg="${weightKg}"></span></div>
                    <div style="display:flex;justify-content:space-between;padding:5px 0;border-bottom:1px solid #e2e2e2;font-size:12px;"><span>Auction Rate:</span> <span>${rateFormatted}</span></div>
                    <div class="final" style="display:flex;justify-content:space-between;padding:7px 0;font-weight:700;font-size:14px;border-top:1px solid #161616;margin-top:4px;"><span>Total Receivable:</span> <strong style="color:#0f5132;">${totalFormatted}</strong></div>
                </div>
            </div>

            <div class="pp-sign" style="margin-top:50px;">
                <div>Buyer Signature (خریدار)</div>
                <div>Munshi / Auctioneer (منشی)</div>
                <div>Fishery Aarhat Stamp (مہر)</div>
            </div>

            <div class="pp-foot" style="margin-top:20px;">
                <div>Official computer-generated Aarhat Lot Catch Slip · Zahid Hussain &amp; Sons · Sukkur Fish Harbour.</div>
                <div style="margin-top:6px;font-size:10.5px;color:#666;border-top:1px dashed #dedede;padding-top:6px;display:flex;justify-content:space-between;align-items:center;flex-wrap:gap:4px 12px;">
                    <span>Software Design &amp; Develope: <strong style="color:#222;">ICT Systems PVT Ltd</strong></span>
                    <span>Mob: <strong style="color:#222;">+92 3313223528</strong></span>
                    <span>Email: <strong style="color:#222;">sales@ictsystems.com.pk</strong></span>
                </div>
            </div>
        `;

        openPrintModal("Bill Item Report — " + lotNo, content);
    }

    // Print Customer Auction Purchase Bill (Parchi / Khari Raseed) - Single or Multi-Lot
    function printCustomerBillInvoice(billNo, customerName, supplierName, date, lots, voucherNo, status, isPaid) {
        const lotList = lots || [];
        const totalLots = lotList.length;
        const totalWeightKg = lotList.reduce((sum, l) => sum + (parseFloat(l.weightKg || l.weight) || 0), 0);
        const totalWeightMaundStr = formatKgToMaund(totalWeightKg);
        const totalAmount = lotList.reduce((sum, l) => sum + (parseFloat(l.amount) || ((parseFloat(l.weightKg || l.weight) || 0) * (parseFloat(l.ratePerKg || l.rate) || 0))), 0);
        const avgRate = totalWeightKg > 0 ? (totalAmount / totalWeightKg) : 0;
        const avgRateMaund = avgRate * 40;

        const rowsHtml = lotList.map((l, i) => {
            const wKg = parseFloat(l.weightKg || l.weight) || 0;
            const rate = parseFloat(l.ratePerKg || l.rate) || 0;
            const amt = parseFloat(l.amount) || (wKg * rate);
            const wStr = l.weightStr || formatKgToMaund(wKg);
            return `
                <tr>
                    <td style="text-align:center;font-size:12px;color:#555;">${i + 1}</td>
                    <td style="text-align:center;font-family:monospace;font-weight:700;font-size:12.5px;">${l.lotNo || ('LOT-' + (i + 1))}</td>
                    <td style="word-break:break-word;font-size:13px;font-weight:600;color:#111;">${l.fishName || l.fish || 'Fish'}</td>
                    <td style="text-align:center;font-family:monospace;font-weight:700;color:#8a5a1e;font-size:13px;">${wStr}</td>
                    <td style="text-align:right;font-size:12.5px;">Rs ${rate.toLocaleString()}</td>
                    <td style="text-align:right;font-weight:700;font-size:13.5px;color:#111;">Rs ${Math.round(amt).toLocaleString()}</td>
                </tr>
            `;
        }).join('');

        const content = `
            <div class="pp-head">
                <div class="pp-brand" style="flex:1;">
                    <div style="font-size:25px;font-weight:900;letter-spacing:0.5px;line-height:1.15;color:#111;">
                        ZAHID HUSSAIN <span style="color:#8a5a1e;">&amp; SONS</span>
                    </div>
                    <div class="sub" style="font-size:12px;color:#444;font-weight:600;margin-top:3px;">Fish Commission Agents · Sukkur Fish Harbour</div>
                    <div class="sub" style="font-size:11.5px;color:#555;font-weight:600;margin-top:2px;">Owner: Zahid Hussain (0300-3110890) | Munshi: Yaseen (0334-3942837)</div>
                </div>
                <div class="pp-doc-title">
                    <h2>CUSTOMER PURCHASE BILL (PARCHI)</h2>
                    <div style="font-size:12px;font-weight:700;color:#8a5a1e;margin-top:2px;">خریدار کھری رسید (نیلام پرچی)</div>
                    <div class="sub" style="margin-top:4px;">Bill #: <strong>${billNo || 'BL-0000'}</strong></div>
                </div>
            </div>

            <div class="pp-meta" style="grid-template-columns: 1.8fr 1.2fr;gap:16px;background:#fcfbf8;border:1px solid #e7dfd0;padding:12px 16px;border-radius:6px;align-items:center;margin-bottom:14px;">
                <div>
                    <b style="font-size:10px;color:#777;letter-spacing:.04em;text-transform:uppercase;margin-bottom:3px;display:block;">Customer (Buyer / خریدار)</b>
                    <strong style="color:#0f5132;font-size:14px;display:block;word-break:break-word;">${customerName || 'General Buyer'}</strong>
                </div>
                <div style="text-align:right;">
                    <b style="font-size:10px;color:#777;letter-spacing:.04em;text-transform:uppercase;margin-bottom:3px;display:block;">Date / تاریخ</b>
                    <span style="font-weight:700;font-size:13.5px;color:#111;">${date || new Date().toLocaleDateString('en-GB')}</span>
                </div>
            </div>

            <table class="pp-table" style="width:100%;table-layout:fixed;border-collapse:collapse;">
                <thead>
                    <tr>
                        <th style="width:32px;text-align:center;">#</th>
                        <th style="width:82px;text-align:center;">Lot #</th>
                        <th style="width:30%;text-align:left;">Fish Variety (جنس)</th>
                        <th style="width:22%;text-align:center;">Weight (Md-Kg-P)</th>
                        <th style="width:18%;text-align:right;">Rate / Kg</th>
                        <th style="width:20%;text-align:right;">Total (Rs)</th>
                    </tr>
                </thead>
                <tbody>
                    ${rowsHtml || `<tr><td colspan="6" style="text-align:center;padding:12px;">No lots found for customer.</td></tr>`}
                </tbody>
                <tfoot>
                    <tr style="background:#f4f3ed;border-top:2px solid #222;">
                        <td colspan="3" style="text-align:right;font-weight:700;font-size:12.5px;padding:8px 6px;">Total Weight &amp; Receivable:</td>
                        <td style="text-align:center;font-family:monospace;font-weight:800;font-size:14px;color:#8a5a1e;padding:8px 6px;">${totalWeightMaundStr}</td>
                        <td style="text-align:right;white-space:nowrap;padding:8px 6px;">
                            <div style="font-weight:700;font-size:12.5px;color:#8a5a1e;line-height:1.2;">Rs ${avgRate.toFixed(2)} <span style="font-size:10px;color:#555;font-weight:600;">/ Kg</span></div>
                            <div style="font-weight:800;font-size:11.5px;color:#0f5132;margin-top:2px;line-height:1.2;">Rs ${avgRateMaund.toLocaleString('en-US', { minimumFractionDigits: 1, maximumFractionDigits: 2 })} <span style="font-size:9.5px;color:#555;font-weight:600;">/ Md</span></div>
                            <div style="font-size:9.5px;color:#666;font-weight:normal;">(Avg Rate / اوسط ریٹ)</div>
                        </td>
                        <td style="text-align:right;font-weight:800;font-size:15px;color:#111;padding:8px 6px;">${fmtRs(totalAmount)}</td>
                    </tr>
                </tfoot>
            </table>

            <div class="pp-totals" style="margin-top:14px;width:350px;margin-left:auto;">
                <div style="display:flex;justify-content:space-between;align-items:center;padding:5px 0;border-bottom:1px solid #e2e2e2;font-size:12.5px;">
                    <span>Purchased Lots:</span>
                    <strong>${totalLots} lot(s)</strong>
                </div>
                <div style="display:flex;justify-content:space-between;align-items:center;padding:5px 0;border-bottom:1px solid #e2e2e2;font-size:12.5px;">
                    <span>Total Fish Weight:</span>
                    <strong style="color:#0f5132;font-family:monospace;font-size:13.5px;">${totalWeightMaundStr}</strong>
                </div>
                <div class="final" style="display:flex;justify-content:space-between;align-items:center;padding:8px 0;font-size:14.5px;font-weight:700;border-top:1px solid #161616;border-bottom:2px solid #161616;margin-top:2px;">
                    <span style="white-space:nowrap;">Total Receivable (واجب الادا):</span>
                    <strong style="white-space:nowrap;font-size:16px;color:#111;font-family:monospace;">${fmtRs(totalAmount)}</strong>
                </div>
            </div>

            <div class="pp-sign" style="margin-top:40px;">
                <div>Buyer Signature (خریدار)</div>
                <div>Munshi / Auctioneer (منشی)</div>
                <div>Fishery Aarhat Stamp (مہر)</div>
            </div>

            <div class="pp-foot" style="margin-top:20px;">
                <div>Official computer-generated Aarhat Customer Purchase Slip · Zahid Hussain &amp; Sons · Sukkur Fish Harbour.</div>
                <div style="margin-top:6px;font-size:10.5px;color:#666;border-top:1px dashed #dedede;padding-top:6px;display:flex;justify-content:space-between;align-items:center;flex-wrap:wrap;gap:4px 12px;">
                    <span>Software Design &amp; Develope: <strong style="color:#222;">ICT Systems PVT Ltd</strong></span>
                    <span>Mob: <strong style="color:#222;">+92 3313223528</strong></span>
                    <span>Email: <strong style="color:#222;">sales@ictsystems.com.pk</strong></span>
                </div>
            </div>
        `;

        openPrintModal("Customer Bill — " + billNo, content);
    }

    // Print Expense Voucher
    function printExpenseVoucher(voucherNo, date, category, desc, amount, paidVia, chequeNo) {
        const chequeHtml = chequeNo ? `<div><b>Cheque / Ref #:</b> <strong>${chequeNo}</strong></div>` : '';
        const content = `
            <div class="pp-head">
                <div class="pp-brand">
                    <div style="font-size:25px;font-weight:900;letter-spacing:0.5px;line-height:1.15;color:#111;">
                        ZAHID HUSSAIN <span style="color:#8a5a1e;">&amp; SONS</span>
                    </div>
                    <div class="sub" style="font-size:12px;color:#444;font-weight:600;margin-top:3px;">Fish Commission Agents · Sukkur Fish Harbour</div>
                    <div class="sub" style="font-size:11.5px;color:#555;font-weight:600;margin-top:2px;"> Zahid Hussain (0300-3110890) | Yaseen (0334-3942837)</div>
                </div>
                <div class="pp-doc-title">
                    <h2>EXPENSE VOUCHER</h2>
                    <div class="sub">Voucher #: <strong>${voucherNo || 'EV-0451'}</strong></div>
                </div>
            </div>

            <div class="pp-meta">
                <div><b>Date:</b> ${date}</div>
                <div><b>Expense Head:</b> <strong>${category}</strong></div>
                <div><b>Paid Via:</b> <span class="badge" style="background:#fdf6ec;color:#8a5a1e;border:1px solid #eedec5;font-weight:600;padding:3px 10px;border-radius:12px;font-size:12px;display:inline-block;">${paidVia}</span></div>
            </div>

            <div style="background:#f9f9f9;border:1px solid #ddd;padding:16px;border-radius:6px;margin-bottom:20px;">
                <div style="font-size:11px;color:#777;text-transform:uppercase;font-weight:600;margin-bottom:4px;">Particulars / Description:</div>
                <div style="font-size:14px;color:#111;">${desc || 'Daily aarhat operations expense'}</div>
            </div>

            <div class="pp-totals">
                <div class="final" style="font-size:17px;">
                    <span>Total Amount Paid:</span>
                    <strong>${fmtRs(amount)}</strong>
                </div>
            </div>

            <div class="pp-sign" style="margin-top:60px;">
                <div>Paid By (Cashier)</div>
                <div>Received By</div>
                <div>Authorized Signature</div>
            </div>
        `;
        openPrintModal("Print Voucher — " + voucherNo, content);
    }

    // Print Receipt Voucher
    function printReceiptVoucher(voucherNo, date, fromAccount, desc, amount, receivedVia, chequeNo, receiptType) {
        const chequeHtml = chequeNo ? `<div><b>Cheque / Ref #:</b> <strong>${chequeNo}</strong></div>` : '';
        const typeHtml = receiptType ? `<div><b>Receipt Type:</b> <span class="badge badge-brass" style="color:#000;">${receiptType}</span></div>` : '';
        const content = `
            <div class="pp-head">
                <div class="pp-brand">
                    <div style="font-size:25px;font-weight:900;letter-spacing:0.5px;line-height:1.15;color:#111;">
                        ZAHID HUSSAIN <span style="color:#8a5a1e;">&amp; SONS</span>
                    </div>
                    <div class="sub" style="font-size:12px;color:#444;font-weight:600;margin-top:3px;">Fish Commission Agents · Sukkur Fish Harbour</div>
                    <div class="sub" style="font-size:11.5px;color:#555;font-weight:600;margin-top:2px;"> Zahid Hussain (0300-3110890) |  Yaseen (0334-3942837)</div>
                </div>
                <div class="pp-doc-title">
                    <h2>RECEIPT VOUCHER</h2>
                    <div class="sub">Voucher #: <strong>${voucherNo || 'CRV-0001'}</strong></div>
                </div>
            </div>

            <div class="pp-meta">
                <div><b>Date:</b> ${date}</div>
                <div><b>Received From:</b> <strong>${fromAccount}</strong></div>
                ${typeHtml}
                <div><b>Channel:</b> <span class="badge badge-brass" style="color:#000;">${receivedVia}</span></div>
                ${chequeHtml}
            </div>

            <div style="background:#f9f9f9;border:1px solid #ddd;padding:16px;border-radius:6px;margin-bottom:20px;">
                <div style="font-size:11px;color:#777;text-transform:uppercase;font-weight:600;margin-bottom:4px;">Particulars / Description:</div>
                <div style="font-size:14px;color:#111;">${desc || 'Receipt from customer / party'}</div>
            </div>

            <div class="pp-totals">
                <div class="final" style="font-size:17px;">
                    <span>Total Amount Received:</span>
                    <strong style="color:var(--green-text,#16a34a);">${fmtRs(amount)}</strong>
                </div>
            </div>

            <div class="pp-sign" style="margin-top:60px;">
                <div>Received By (Cashier)</div>
                <div>Deposited / Paid By</div>
                <div>Authorized Signature</div>
            </div>
        `;
        openPrintModal("Print Receipt — " + voucherNo, content);
    }

    // Print Payment Voucher
    function printPaymentVoucher(voucherNo, date, toAccount, desc, amount, paidVia, chequeNo, paymentType) {
        const chequeHtml = chequeNo ? `<div><b>Cheque / Ref #:</b> <strong>${chequeNo}</strong></div>` : '';
        const typeHtml = paymentType ? `<div><b>Payment Type:</b> <span class="badge badge-brass" style="color:#000;">${paymentType}</span></div>` : '';
        const content = `
            <div class="pp-head">
                <div class="pp-brand">
                    <div style="font-size:25px;font-weight:900;letter-spacing:0.5px;line-height:1.15;color:#111;">
                        ZAHID HUSSAIN <span style="color:#8a5a1e;">&amp; SONS</span>
                    </div>
                    <div class="sub" style="font-size:12px;color:#444;font-weight:600;margin-top:3px;">Fish Commission Agents · Sukkur Fish Harbour</div>
                    <div class="sub" style="font-size:11.5px;color:#555;font-weight:600;margin-top:2px;"> Zahid Hussain (0300-3110890) | Yaseen (0334-3942837)</div>
                </div>
                <div class="pp-doc-title">
                    <h2>PAYMENT VOUCHER</h2>
                    <div class="sub">Voucher #: <strong>${voucherNo || 'CPV-0001'}</strong></div>
                </div>
            </div>

            <div class="pp-meta">
                <div><b>Date:</b> ${date}</div>
                <div><b>Paid To:</b> <strong>${toAccount}</strong></div>
                ${typeHtml}
                <div><b>Channel:</b> <span class="badge badge-brass" style="color:#000;">${paidVia}</span></div>
                ${chequeHtml}
            </div>

            <div style="background:#f9f9f9;border:1px solid #ddd;padding:16px;border-radius:6px;margin-bottom:20px;">
                <div style="font-size:11px;color:#777;text-transform:uppercase;font-weight:600;margin-bottom:4px;">Particulars / Description:</div>
                <div style="font-size:14px;color:#111;">${desc || 'Payment to supplier / party'}</div>
            </div>

            <div class="pp-totals">
                <div class="final" style="font-size:17px;">
                    <span>Total Amount Paid:</span>
                    <strong style="color:var(--red-text,#dc2626);">${fmtRs(amount)}</strong>
                </div>
            </div>

            <div class="pp-sign" style="margin-top:60px;">
                <div>Paid By (Cashier)</div>
                <div>Received By</div>
                <div>Authorized Signature</div>
            </div>
        `;
        openPrintModal("Print Payment — " + voucherNo, content);
    }

    // Print Account Statement
    function printLedgerStatement(accountName, fromDate, toDate, totalDebit, totalCredit, closingBal, entries, totalAdvance, totalMartey, totalBillAmount) {
        const rowsHtml = (entries || []).map(e => `
            <tr>
                <td style="white-space:nowrap;font-size:11px;">${e.date}</td>
                <td style="font-family:monospace;font-weight:600;font-size:11px;word-break:break-all;">${e.ref}</td>
                <td class="col-desc" style="white-space:normal;word-break:break-word;overflow-wrap:anywhere;line-height:1.35;font-size:11.5px;">${e.desc}</td>
                <td style="text-align:right;white-space:nowrap;font-size:11.5px;">${e.debit > 0 ? fmtRs(e.debit) : '-'}</td>
                <td style="text-align:right;white-space:nowrap;font-size:11.5px;">${e.credit > 0 ? fmtRs(e.credit) : '-'}</td>
                <td style="text-align:right;font-weight:700;white-space:nowrap;font-size:11.5px;">${fmtRs(e.balance)}</td>
            </tr>
        `).join('');

        const isSupplier = (accountName && accountName.toLowerCase().includes('supplier')) ||
            totalAdvance !== undefined || totalMartey !== undefined || totalBillAmount !== undefined;

        let advVal = totalAdvance !== undefined && totalAdvance !== null ? Number(totalAdvance) : null;
        let marteyVal = totalMartey !== undefined && totalMartey !== null ? Number(totalMartey) : null;
        let billVal = totalBillAmount !== undefined && totalBillAmount !== null ? Number(totalBillAmount) : null;

        if (isSupplier) {
            if (advVal === null || isNaN(advVal)) {
                advVal = (entries || [])
                    .filter(e => Number(e.debit) > 0 &&
                        (!e.desc ||
                            (!e.desc.toLowerCase().includes('farmer expenses') &&
                                !e.desc.toLowerCase().includes('paid on behalf'))))
                    .reduce((acc, e) => acc + (Number(e.debit) || 0), 0);
            }
            if (marteyVal === null || isNaN(marteyVal)) {
                marteyVal = (entries || []).filter(e => e.desc && (e.desc.toLowerCase().includes('martey') || e.desc.toLowerCase().includes('deposit') || e.desc.includes('مارتی'))).reduce((acc, e) => acc + (Number(e.credit) || Number(e.debit) || 0), 0);
            }
            if (billVal === null || isNaN(billVal)) {
                billVal = (entries || [])
                    .filter(e => Number(e.credit) > 0 && e.desc &&
                        (e.desc.toLowerCase().includes('catch settlement') ||
                            e.desc.toLowerCase().includes('settlement') ||
                            (e.desc.toLowerCase().includes('catch') &&
                                !e.desc.toLowerCase().includes('martey') &&
                                !e.desc.toLowerCase().includes('expense') &&
                                !e.desc.toLowerCase().includes('labour') &&
                                !e.desc.toLowerCase().includes('ice'))))
                    .reduce((acc, e) => acc + (Number(e.credit) || 0), 0);
                if (billVal === 0 && Number(totalCredit) > 0) {
                    billVal = Number(totalCredit);
                }
            }
        }

        const totalsSectionHtml = isSupplier ? `
            <div class="pp-totals-container" style="display:flex;justify-content:space-between;align-items:flex-start;margin-top:14px;border-top:1px solid #ddd;padding-top:10px;">
                <div class="pp-totals-left" style="width:280px;font-size:12.5px;">
                    <div style="display:flex;justify-content:space-between;padding:5px 0;border-bottom:1px solid #e8e8e8;">
                        <span style="color:#555;">Total Advance:</span>
                        <strong style="color:#0f5132;">${fmtRs(advVal)}</strong>
                    </div>
                    <div style="display:flex;justify-content:space-between;padding:5px 0;border-bottom:1px solid #e8e8e8;">
                        <span style="color:#555;">Total Martey:</span>
                        <strong style="color:#8a2be2;">${fmtRs(marteyVal)}</strong>
                    </div>
                    <div style="display:flex;justify-content:space-between;padding:5px 0;border-bottom:1px solid #e8e8e8;">
                        <span style="color:#555;">Total Bill Amount:</span>
                        <strong style="color:#842029;">${fmtRs(billVal)}</strong>
                    </div>
                </div>
                <div class="pp-totals" style="margin:0;width:280px;font-size:12.5px;">
                    <div><span>Total Debit:</span> <strong>${fmtRs(totalDebit)}</strong></div>
                    <div><span>Total Credit:</span> <strong>${fmtRs(totalCredit)}</strong></div>
                    <div class="final"><span>Net Balance:</span> <strong>${fmtRs(closingBal)}</strong></div>
                </div>
            </div>
        ` : `
            <div class="pp-totals">
                <div><span>Total Debit:</span> <strong>${fmtRs(totalDebit)}</strong></div>
                <div><span>Total Credit:</span> <strong>${fmtRs(totalCredit)}</strong></div>
                <div class="final"><span>Net Balance:</span> <strong>${fmtRs(closingBal)}</strong></div>
            </div>
        `;

        const content = `
            <div class="pp-head">
                <div class="pp-brand">
                    <div style="font-size:25px;font-weight:900;letter-spacing:0.5px;line-height:1.15;color:#111;">
                        ZAHID HUSSAIN <span style="color:#8a5a1e;">&amp; SONS</span>
                    </div>
                    <div class="sub" style="font-size:12px;color:#444;font-weight:600;margin-top:3px;">Fish Commission Agents · Sukkur Fish Harbour</div>
                    <div class="sub" style="font-size:11.5px;color:#555;font-weight:600;margin-top:2px;"> Zahid Hussain (0300-3110890) | Yaseen (0334-3942837)</div>
                </div>
                <div class="pp-doc-title">
                    <h2>STATEMENT OF ACCOUNT</h2>
                    <div class="sub">Account: <strong>${accountName}</strong></div>
                </div>
            </div>

            <div class="pp-meta">
                <div><b>Account Title:</b> <strong>${accountName}</strong></div>
                <div><b>Period:</b> ${fromDate || 'Start'} to ${toDate || 'Today'}</div>
                <div><b>Closing Balance:</b> <strong>${fmtRs(closingBal)}</strong></div>
            </div>

            <table class="pp-table" style="width:100%;table-layout:fixed;border-collapse:collapse;">
                <thead>
                    <tr>
                        <th style="width:13%;">Date</th>
                        <th style="width:16%;">Ref #</th>
                        <th style="width:35%;">Description</th>
                        <th style="width:12%;text-align:right;">Debit (Dr)</th>
                        <th style="width:12%;text-align:right;">Credit (Cr)</th>
                        <th style="width:12%;text-align:right;">Balance</th>
                    </tr>
                </thead>
                <tbody>
                    ${rowsHtml || `<tr><td colspan="6" style="text-align:center;">No ledger transactions</td></tr>`}
                </tbody>
            </table>

            ${totalsSectionHtml}

            <div class="pp-sign">
                <div>Prepared By</div>
                <div>Verified By</div>
                <div>Accountant</div>
            </div>
        `;
        openPrintModal("Print Ledger — " + accountName, content);
    }

    // Print Daily or Monthly Expense Report
    function printExpenseReport(reportTitle, periodLabel, accountLabel, entries, totalCount, cashPaid, bankPaid, totalAmount) {
        const rowsHtml = (entries || []).map((e, idx) => `
            <tr>
                <td style="text-align:center;font-size:10.5px;color:#555;">${idx + 1}</td>
                <td style="font-family:monospace;font-weight:700;font-size:10.5px;color:#8a5a1e;white-space:nowrap;">${e.voucherNo || '-'}</td>
                <td style="white-space:nowrap;font-size:10.5px;">${e.date || '-'}</td>
                <td style="font-weight:600;font-size:11px;color:#111;">${e.account || '-'}</td>
                <td class="col-desc" style="font-size:10.5px;color:#333;">${e.desc || '-'}</td>
                <td style="font-size:10.5px;color:#222;white-space:nowrap;">${e.paidVia || '-'}</td>
                <td class="num-cell" style="text-align:right;font-weight:700;font-size:11.5px;color:#a82020;white-space:nowrap !important;">${fmtRs(e.amount)}</td>
            </tr>
        `).join('');

        const content = `
            <style>
                #printModal .print-paper {
                    width: 297mm !important;
                    max-width: 96vw !important;
                    padding: 8mm 12mm !important;
                    box-sizing: border-box !important;
                }
                #printModal .print-modal-bar {
                    width: 297mm !important;
                    max-width: 96vw !important;
                }
                #printModal .pp-table th,
                #printModal .pp-table td {
                    padding: 5px 6px !important;
                }
                @media print {
                    @page {
                        size: A4 landscape;
                        margin: 8mm;
                    }
                    body.printing .print-paper {
                        width: 100% !important;
                        max-width: 100% !important;
                        padding: 0 !important;
                        box-shadow: none !important;
                    }
                }
            </style>

            <div class="pp-head">
                <div class="pp-brand">
                    <div style="font-size:24px;font-weight:900;letter-spacing:0.5px;line-height:1.15;color:#111;">
                        ZAHID HUSSAIN <span style="color:#8a5a1e;">&amp; SONS</span>
                    </div>
                    <div class="sub" style="font-size:11.5px;color:#444;font-weight:600;margin-top:2px;">Fish Commission Agents · Sukkur Fish Harbour</div>
                    <div class="sub" style="font-size:11px;color:#555;font-weight:600;margin-top:2px;"> Zahid Hussain (0300-3110890) |  Yaseen (0334-3942837)</div>
                </div>
                <div class="pp-doc-title">
                    <h2 style="font-size:18px;">${(reportTitle || 'EXPENSE REPORT').toUpperCase()}</h2>
                    <div class="sub">Generated on: <strong>${new Date().toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' })}</strong></div>
                </div>
            </div>

            <div class="pp-meta" style="display:flex;justify-content:space-between;background:#fdfcf9;border:1px solid #eedec5;padding:8px 12px;border-radius:6px;margin-bottom:14px;font-size:11.5px;">
                <div><b>Period / Date:</b> <strong>${periodLabel || 'All Dates'}</strong></div>
                <div><b>Account:</b> <strong>${accountLabel || 'All Accounts'}</strong></div>
                <div><b>Total Vouchers:</b> <strong>${totalCount || (entries ? entries.length : 0)}</strong></div>
            </div>

            <table class="pp-table" style="width:100%;table-layout:fixed;border-collapse:collapse;margin-bottom:16px;">
                <thead>
                    <tr>
                        <th style="width:4%;text-align:center;">#</th>
                        <th style="width:13%;white-space:nowrap;">Voucher #</th>
                        <th style="width:10%;white-space:nowrap;">Date</th>
                        <th style="width:21%;">Expense Head</th>
                        <th style="width:28%;line-height:1.25;vertical-align:middle;">Particulars /<br/>Description</th>
                        <th style="width:12%;white-space:nowrap;">Paid Via</th>
                        <th style="width:12%;text-align:right;white-space:nowrap;">Amount</th>
                    </tr>
                </thead>
                <tbody>
                    ${rowsHtml || `<tr><td colspan="6" style="text-align:center;padding:16px;color:#777;">No expense records found for the selected filter</td></tr>`}
                </tbody>
                <tfoot>
                    <tr style="background:#faf7f2;border-top:2px solid #111;font-weight:700;">
                        <td colspan="4" style="text-align:right;padding-right:10px;">Summary:</td>
                        <td colspan="2" style="font-size:11px;color:#555;">
                            Cash: <strong>${fmtRs(cashPaid)}</strong> &bull; Bank: <strong>${fmtRs(bankPaid)}</strong>
                        </td>
                        <td class="num-cell" style="text-align:right;font-size:12.5px;color:#a82020;white-space:nowrap !important;">${fmtRs(totalAmount)}</td>
                    </tr>
                </tfoot>
            </table>

            <div class="pp-totals" style="margin-top:10px;">
                <div><span>Total Vouchers:</span> <strong>${totalCount || (entries ? entries.length : 0)}</strong></div>
                <div><span>Cash Paid:</span> <strong>${fmtRs(cashPaid)}</strong></div>
                <div><span>Bank Paid:</span> <strong>${fmtRs(bankPaid)}</strong></div>
                <div class="final" style="font-size:15px;"><span>Total Expense Paid:</span> <strong>${fmtRs(totalAmount)}</strong></div>
            </div>

            <div class="pp-sign" style="margin-top:40px;">
                <div>Prepared By</div>
                <div>Cashier / Accountant</div>
                <div>Authorized Signature</div>
            </div>

            <div class="pp-foot" style="margin-top:20px;text-align:center;font-size:9.5px;color:#888;">
                Official Expense Report · Zahid Hussain &amp; Sons · Sukkur Fish Harbour · Generated from WebDesk Aarhat ERP
            </div>
        `;

        openPrintModal((reportTitle || "Expense Report") + " — " + (periodLabel || ""), content);
    }

    // Print Payment Report (matching Expense Report structure)
    function printPaymentReport(reportTitle, periodLabel, accountLabel, entries, totalCount, cashPaid, bankPaid, totalAmount) {
        const rowsHtml = (entries || []).map((e, idx) => `
            <tr>
                <td style="text-align:center;font-size:10.5px;color:#555;">${idx + 1}</td>
                <td style="font-family:monospace;font-weight:700;font-size:10.5px;color:#8a5a1e;white-space:nowrap;">${e.voucherNo || '-'}</td>
                <td style="white-space:nowrap;font-size:10.5px;">${e.date || '-'}</td>
                <td style="font-weight:600;font-size:11px;color:#111;">${e.account || '-'}</td>
                <td style="font-size:10.5px;color:#222;white-space:nowrap;">${e.paymentType || 'General Payment'}</td>
                <td class="col-desc" style="font-size:10.5px;color:#333;">${e.desc || '-'}</td>
                <td style="font-size:10.5px;color:#222;white-space:nowrap;">${e.paidVia || '-'}</td>
                <td class="num-cell" style="text-align:right;font-weight:700;font-size:11.5px;color:#a82020;white-space:nowrap !important;">${fmtRs(e.amount)}</td>
            </tr>
        `).join('');

        const content = `
            <style>
                #printModal .print-paper {
                    width: 297mm !important;
                    max-width: 96vw !important;
                    padding: 8mm 12mm !important;
                    box-sizing: border-box !important;
                }
                #printModal .print-modal-bar {
                    width: 297mm !important;
                    max-width: 96vw !important;
                }
                #printModal .pp-table th,
                #printModal .pp-table td {
                    padding: 5px 6px !important;
                }
                @media print {
                    @page {
                        size: A4 landscape;
                        margin: 8mm;
                    }
                    body.printing .print-paper {
                        width: 100% !important;
                        max-width: 100% !important;
                        padding: 0 !important;
                        box-shadow: none !important;
                    }
                }
            </style>

            <div class="pp-head">
                <div class="pp-brand">
                    <div style="font-size:24px;font-weight:900;letter-spacing:0.5px;line-height:1.15;color:#111;">
                        ZAHID HUSSAIN <span style="color:#8a5a1e;">&amp; SONS</span>
                    </div>
                    <div class="sub" style="font-size:11.5px;color:#444;font-weight:600;margin-top:2px;">Fish Commission Agents · Sukkur Fish Harbour</div>
                    <div class="sub" style="font-size:11px;color:#555;font-weight:600;margin-top:2px;"> Zahid Hussain (0300-3110890) |  Yaseen (0334-3942837)</div>
                </div>
                <div class="pp-doc-title">
                    <h2 style="font-size:18px;">${(reportTitle || 'PAYMENT REPORT').toUpperCase()}</h2>
                    <div class="sub">Generated on: <strong>${new Date().toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' })}</strong></div>
                </div>
            </div>

            <div class="pp-meta" style="display:flex;justify-content:space-between;background:#fdfcf9;border:1px solid #eedec5;padding:8px 12px;border-radius:6px;margin-bottom:14px;font-size:11.5px;">
                <div><b>Period / Date:</b> <strong>${periodLabel || 'All Dates'}</strong></div>
                <div><b>Account:</b> <strong>${accountLabel || 'All Accounts'}</strong></div>
                <div><b>Total Vouchers:</b> <strong>${totalCount || (entries ? entries.length : 0)}</strong></div>
            </div>

            <table class="pp-table" style="width:100%;table-layout:fixed;border-collapse:collapse;margin-bottom:16px;">
                <thead>
                    <tr>
                        <th style="width:4%;text-align:center;">#</th>
                        <th style="width:13%;white-space:nowrap;">Voucher #</th>
                        <th style="width:10%;white-space:nowrap;">Date</th>
                        <th style="width:17%;">Paid To</th>
                        <th style="width:10%;white-space:nowrap;">Type</th>
                        <th style="width:22%;line-height:1.25;vertical-align:middle;">Particulars /<br/>Description</th>
                        <th style="width:12%;white-space:nowrap;">Channel</th>
                        <th style="width:12%;text-align:right;white-space:nowrap;">Amount</th>
                    </tr>
                </thead>
                <tbody>
                    ${rowsHtml || `<tr><td colspan="8" style="text-align:center;padding:16px;color:#777;">No payment records found for the selected filter</td></tr>`}
                </tbody>
                <tfoot>
                    <tr style="background:#faf7f2;border-top:2px solid #111;font-weight:700;">
                        <td colspan="5" style="text-align:right;padding-right:10px;">Summary:</td>
                        <td colspan="2" style="font-size:11px;color:#555;">
                            Cash: <strong>${fmtRs(cashPaid)}</strong> &bull; Bank: <strong>${fmtRs(bankPaid)}</strong>
                        </td>
                        <td class="num-cell" style="text-align:right;font-size:12.5px;color:#a82020;white-space:nowrap !important;">${fmtRs(totalAmount)}</td>
                    </tr>
                </tfoot>
            </table>

            <div class="pp-totals" style="margin-top:10px;">
                <div><span>Total Vouchers:</span> <strong>${totalCount || (entries ? entries.length : 0)}</strong></div>
                <div><span>Cash Paid:</span> <strong>${fmtRs(cashPaid)}</strong></div>
                <div><span>Bank Paid:</span> <strong>${fmtRs(bankPaid)}</strong></div>
                <div class="final" style="font-size:15px;"><span>Total Amount Paid:</span> <strong>${fmtRs(totalAmount)}</strong></div>
            </div>

            <div class="pp-sign" style="margin-top:40px;">
                <div>Prepared By</div>
                <div>Cashier / Accountant</div>
                <div>Authorized Signature</div>
            </div>

            <div class="pp-foot" style="margin-top:20px;text-align:center;font-size:9.5px;color:#888;">
                Official Payment Report · Zahid Hussain &amp; Sons · Sukkur Fish Harbour · Generated from WebDesk Aarhat ERP
            </div>
        `;

        openPrintModal((reportTitle || "Payment Report") + " — " + (periodLabel || ""), content);
    }

    // Print Receipt Report (matching Expense & Payment Report structure)
    function printReceiptReport(reportTitle, periodLabel, accountLabel, entries, totalCount, cashReceived, bankReceived, totalAmount) {
        const rowsHtml = (entries || []).map((e, idx) => `
            <tr>
                <td style="text-align:center;font-size:10.5px;color:#555;">${idx + 1}</td>
                <td style="font-family:monospace;font-weight:700;font-size:10.5px;color:#8a5a1e;white-space:nowrap;">${e.voucherNo || '-'}</td>
                <td style="white-space:nowrap;font-size:10.5px;">${e.date || '-'}</td>
                <td style="font-weight:600;font-size:11px;color:#111;">${e.account || '-'}</td>
                <td style="font-size:10.5px;color:#222;white-space:nowrap;">${e.receiptType || 'General Receipt'}</td>
                <td class="col-desc" style="font-size:10.5px;color:#333;">${e.desc || '-'}</td>
                <td style="font-size:10.5px;color:#222;white-space:nowrap;">${e.receivedVia || '-'}</td>
                <td class="num-cell" style="text-align:right;font-weight:700;font-size:11.5px;color:#15803d;white-space:nowrap !important;">${fmtRs(e.amount)}</td>
            </tr>
        `).join('');

        const content = `
            <style>
                #printModal .print-paper {
                    width: 297mm !important;
                    max-width: 96vw !important;
                    padding: 8mm 12mm !important;
                    box-sizing: border-box !important;
                }
                #printModal .print-modal-bar {
                    width: 297mm !important;
                    max-width: 96vw !important;
                }
                #printModal .pp-table th,
                #printModal .pp-table td {
                    padding: 5px 6px !important;
                }
                @media print {
                    @page {
                        size: A4 landscape;
                        margin: 8mm;
                    }
                    body.printing .print-paper {
                        width: 100% !important;
                        max-width: 100% !important;
                        padding: 0 !important;
                        box-shadow: none !important;
                    }
                }
            </style>

            <div class="pp-head">
                <div class="pp-brand">
                    <div style="font-size:24px;font-weight:900;letter-spacing:0.5px;line-height:1.15;color:#111;">
                        ZAHID HUSSAIN <span style="color:#8a5a1e;">&amp; SONS</span>
                    </div>
                    <div class="sub" style="font-size:11.5px;color:#444;font-weight:600;margin-top:2px;">Fish Commission Agents · Sukkur Fish Harbour</div>
                    <div class="sub" style="font-size:11px;color:#555;font-weight:600;margin-top:2px;"> Zahid Hussain (0300-3110890) |  Yaseen (0334-3942837)</div>
                </div>
                <div class="pp-doc-title">
                    <h2 style="font-size:18px;">${(reportTitle || 'RECEIPT REPORT').toUpperCase()}</h2>
                    <div class="sub">Generated on: <strong>${new Date().toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' })}</strong></div>
                </div>
            </div>

            <div class="pp-meta" style="display:flex;justify-content:space-between;background:#fdfcf9;border:1px solid #eedec5;padding:8px 12px;border-radius:6px;margin-bottom:14px;font-size:11.5px;">
                <div><b>Period / Date:</b> <strong>${periodLabel || 'All Dates'}</strong></div>
                <div><b>Account:</b> <strong>${accountLabel || 'All Accounts'}</strong></div>
                <div><b>Total Vouchers:</b> <strong>${totalCount || (entries ? entries.length : 0)}</strong></div>
            </div>

            <table class="pp-table" style="width:100%;table-layout:fixed;border-collapse:collapse;margin-bottom:16px;">
                <thead>
                    <tr>
                        <th style="width:4%;text-align:center;">#</th>
                        <th style="width:13%;white-space:nowrap;">Voucher #</th>
                        <th style="width:10%;white-space:nowrap;">Date</th>
                        <th style="width:17%;">Received From</th>
                        <th style="width:10%;white-space:nowrap;">Type</th>
                        <th style="width:22%;line-height:1.25;vertical-align:middle;">Particulars /<br/>Description</th>
                        <th style="width:12%;white-space:nowrap;">Channel</th>
                        <th style="width:12%;text-align:right;white-space:nowrap;">Amount</th>
                    </tr>
                </thead>
                <tbody>
                    ${rowsHtml || `<tr><td colspan="8" style="text-align:center;padding:16px;color:#777;">No receipt records found for the selected filter</td></tr>`}
                </tbody>
                <tfoot>
                    <tr style="background:#faf7f2;border-top:2px solid #111;font-weight:700;">
                        <td colspan="5" style="text-align:right;padding-right:10px;">Summary:</td>
                        <td colspan="2" style="font-size:11px;color:#555;">
                            Cash: <strong>${fmtRs(cashReceived)}</strong> &bull; Bank: <strong>${fmtRs(bankReceived)}</strong>
                        </td>
                        <td class="num-cell" style="text-align:right;font-size:12.5px;color:#15803d;white-space:nowrap !important;">${fmtRs(totalAmount)}</td>
                    </tr>
                </tfoot>
            </table>

            <div class="pp-totals" style="margin-top:10px;">
                <div><span>Total Vouchers:</span> <strong>${totalCount || (entries ? entries.length : 0)}</strong></div>
                <div><span>Cash Received:</span> <strong>${fmtRs(cashReceived)}</strong></div>
                <div><span>Bank Received:</span> <strong>${fmtRs(bankReceived)}</strong></div>
                <div class="final" style="font-size:15px;"><span>Total Amount Received:</span> <strong>${fmtRs(totalAmount)}</strong></div>
            </div>

            <div class="pp-sign" style="margin-top:40px;">
                <div>Prepared By</div>
                <div>Cashier / Accountant</div>
                <div>Authorized Signature</div>
            </div>

            <div class="pp-foot" style="margin-top:20px;text-align:center;font-size:9.5px;color:#888;">
                Official Receipt Report · Zahid Hussain &amp; Sons · Sukkur Fish Harbour · Generated from WebDesk Aarhat ERP
            </div>
        `;

        openPrintModal((reportTitle || "Receipt Report") + " — " + (periodLabel || ""), content);
    }

    // --------------------------------------------------------------------------
    // CHART OF ACCOUNTS (Tree & Interactive Drill-down)
    // --------------------------------------------------------------------------
    const defaultCoa = [
        {
            code: '1000', name: 'Assets', balance: 2415500, children: [
                { code: '1010', name: 'Cash in Hand', balance: 340000, leaf: true },
                { code: '1020', name: 'Bank Balance', balance: 680500, leaf: true },
                {
                    code: '1030', name: 'Customer Receivables', balance: 1280000, children: [
                        { code: '1031', name: 'Al-Bahar Hotel', balance: 284000, leaf: true },
                        { code: '1032', name: 'Seafood Express (Pvt) Ltd', balance: 461500, leaf: true },
                        { code: '1033', name: 'Do Darya Restaurant', balance: 96200, leaf: true },
                        { code: '1034', name: 'Karachi Export Traders', balance: 377300, leaf: true }
                    ]
                },
                { code: '1040', name: 'Stock (Unsold Lots)', balance: 115000, leaf: true }
            ]
        },
        {
            code: '2000', name: 'Liabilities', balance: 964600, children: [
                {
                    code: '2010', name: 'Supplier Payables', balance: 940000, children: [
                        { code: '2011', name: 'Younus Boat Co.', balance: 44000, leaf: true },
                        { code: '2012', name: 'Rasool Bux Mallah', balance: 71200, leaf: true },
                        { code: '2013', name: 'Haji Karim Baksh', balance: 96400, leaf: true },
                        { code: '2014', name: 'Sikandar Fisheries', balance: 58900, leaf: true }
                    ]
                },
                { code: '2020', name: 'Outstanding Expenses', balance: 24600, leaf: true }
            ]
        },
        { code: '3000', name: "Owner's Equity", balance: 1450900, leaf: true },
        {
            code: '4000', name: 'Income', balance: 1096900, children: [
                { code: '4010', name: 'Commission Earned', balance: 984600, leaf: true },
                { code: '4020', name: 'Expense Recovery from Suppliers', balance: 112300, leaf: true }
            ]
        },
        {
            code: '5000', name: 'Expenses', balance: 177500, children: [
                { code: '5010', name: 'Labour / Tulai', balance: 68400, leaf: true },
                { code: '5020', name: 'Loading / Transport', balance: 51200, leaf: true },
                { code: '5030', name: 'Ice & Packing', balance: 22900, leaf: true },
                { code: '5040', name: 'Rent & Utilities', balance: 35000, leaf: true }
            ]
        }
    ];

    function renderCoaTree(containerId, treeData) {
        const container = document.getElementById(containerId);
        if (!container) return;

        const data = treeData || defaultCoa;

        function buildNode(node) {
            const hasChildren = node.children && node.children.length > 0;
            const li = document.createElement('li');
            li.className = 'coa-node';

            const row = document.createElement('div');
            row.className = 'coa-node-row';
            row.onclick = function (e) {
                e.stopPropagation();
                document.querySelectorAll('.coa-node-row').forEach(r => r.classList.remove('selected'));
                row.classList.add('selected');

                if (hasChildren) {
                    const twist = row.querySelector('.twist');
                    const childUl = li.querySelector('.coa-children');
                    if (twist && childUl) {
                        twist.classList.toggle('open');
                        childUl.classList.toggle('open');
                    }
                }
                showCoaDetail(node);
            };

            row.innerHTML = `
                ${hasChildren ? '<span class="twist open"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="m9 18 6-6-6-6"/></svg></span>' : '<span class="dot"></span>'}
                <span class="nm ${hasChildren ? 'grp' : ''}">${node.name}</span>
                <span class="code">[${node.code}]</span>
                <span class="bal tnum">${fmtRs(node.balance)}</span>
            `;
            li.appendChild(row);

            if (hasChildren) {
                const ul = document.createElement('ul');
                ul.className = 'coa-children open';
                node.children.forEach(child => ul.appendChild(buildNode(child)));
                li.appendChild(ul);
            }

            return li;
        }

        const rootUl = document.createElement('ul');
        rootUl.className = 'coa-tree';
        data.forEach(n => rootUl.appendChild(buildNode(n)));
        container.innerHTML = '';
        container.appendChild(rootUl);
    }

    function showCoaDetail(node) {
        const card = document.getElementById('coaDetailCard');
        if (!card) return;

        card.innerHTML = `
            <div class="card-head">
                <div>
                    <h3 style="font-size:17px;color:var(--text);">${node.name}</h3>
                    <div style="font-size:12px;color:var(--text-faint);">Account Code: <strong>${node.code}</strong> · Type: ${node.leaf ? 'Ledger Account' : 'Control Account'}</div>
                </div>
                <button class="btn brass" onclick="AarhatApp.printLedgerStatement('${node.name}', '2026-08-01', '2026-08-31', ${node.balance}, 0, ${node.balance}, [
                    {date:'31-Aug-2026', ref:'OPN-01', desc:'Opening / Settled Balance', debit:${node.balance}, credit:0, balance:${node.balance}}
                ])">
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M6 9V2h12v7M6 18h12v4H6zM6 14h12"/></svg>Print Account
                </button>
            </div>
            <div class="report-stat-row" style="margin-bottom:16px;">
                <div class="report-stat"><div class="lbl">CURRENT BALANCE</div><div class="val tnum" style="color:var(--brass-text);">${fmtRs(node.balance)}</div></div>
                <div class="report-stat"><div class="lbl">STATUS</div><div class="val" style="font-size:15px;color:var(--green-text);">Active</div></div>
            </div>
            <div class="table-wrap">
                <table>
                    <thead>
                        <tr><th>Date</th><th>Ref #</th><th>Description</th><th style="text-align:right;">Debit</th><th style="text-align:right;">Credit</th><th style="text-align:right;">Balance</th></tr>
                    </thead>
                    <tbody>
                        <tr>
                            <td>31-Aug-2026</td>
                            <td><span class="tcode">SYS-BAL</span></td>
                            <td>Current verified ledger posting</td>
                            <td style="text-align:right;" class="tnum">${fmtRs(node.balance)}</td>
                            <td style="text-align:right;" class="tnum">-</td>
                            <td style="text-align:right;font-weight:600;" class="tnum">${fmtRs(node.balance)}</td>
                        </tr>
                    </tbody>
                </table>
            </div>
        `;
    }

    // --------------------------------------------------------------------------
    // INIT ON DOM READY
    // --------------------------------------------------------------------------
    document.addEventListener('DOMContentLoaded', function () {
        // Init initial bill calculations if present on page
        if (document.getElementById('billLotBody')) {
            recalcBill();
        }
        // Init COA tree if present
        if (document.getElementById('coaTree')) {
            renderCoaTree('coaTree');
        }
    });

    return {
        fmtRs,
        toggleForm,
        toggleSidebar,
        recalcBill,
        addBillLotRow,
        removeBillLotRow,
        showSubTab,
        openPrintModal,
        openPrint: openPrintModal,
        closePrint,
        doPrint,
        printBillInvoice,
        printCustomerBillInvoice,
        printBillItemReport,
        printCurrentBill,
        printExpenseVoucher,
        printReceiptVoucher,
        printPaymentVoucher,
        printExpenseReport,
        printPaymentReport,
        printReceiptReport,
        printLedgerStatement,
        renderCoaTree,
        showCoaDetail
    };

})();
