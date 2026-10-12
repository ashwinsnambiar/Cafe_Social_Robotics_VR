using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace TrialLogging
{
    public static class TrialReportExporter
    {
        public static void SaveAllExports(TrialSessionRecord session, string outputDirectory)
        {
            if (session == null) return;
            if (!Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            session.CalculateOverallSummary();
            foreach (var set in session.sets)
            {
                set.CalculateSummary();
            }

            string baseFileName = string.IsNullOrEmpty(session.sessionId) ? $"Trial_{session.subjectId}" : session.sessionId;

            // 1. Orders Detailed CSV
            string ordersCsvPath = Path.Combine(outputDirectory, $"{baseFileName}_Orders_Detailed.csv");
            File.WriteAllText(ordersCsvPath, GenerateOrdersCsv(session), Encoding.UTF8);

            // 2. Sets Summary CSV
            string setsCsvPath = Path.Combine(outputDirectory, $"{baseFileName}_Sets_Summary.csv");
            File.WriteAllText(setsCsvPath, GenerateSetsCsv(session), Encoding.UTF8);

            // 3. Trial Summary CSV
            string trialCsvPath = Path.Combine(outputDirectory, $"{baseFileName}_Trial_Summary.csv");
            File.WriteAllText(trialCsvPath, GenerateTrialSummaryCsv(session), Encoding.UTF8);

            // 4. HTML Report (human-readable & print to PDF)
            string htmlPath = Path.Combine(outputDirectory, $"{baseFileName}_Trial_Report.html");
            File.WriteAllText(htmlPath, GenerateHtmlReport(session), Encoding.UTF8);

            // 5. Markdown Report
            string mdPath = Path.Combine(outputDirectory, $"{baseFileName}_Trial_Report.md");
            File.WriteAllText(mdPath, GenerateMarkdownReport(session), Encoding.UTF8);

            try
            {
                Debug.Log($"<color=green>[TrialReportExporter]</color> All trial reports saved successfully to:\n<b>{outputDirectory}</b>");
            }
            catch
            {
                Console.WriteLine($"[TrialReportExporter] All trial reports saved successfully to: {outputDirectory}");
            }
        }

        public static string GenerateOrdersCsv(TrialSessionRecord session)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Session_ID,Subject_ID,Workload_Condition,Set_Number,Set_Name,Order_In_Set,Overall_Order_Number,Order_Title,Calorie_Limit,Sugar_Limit,Fat_Limit,Target_Table,Selected_Table,Table_Match,Required_Items,Assembled_Items,Dish_Match,Fully_Correct,Missing_Items,Extra_Items,Ticked_On_Board,Spawn_Time,Tray_Secured_Time,Dispatch_Time,Delivery_Complete_Time,Time_To_Dispatch_Sec,Time_To_Tray_Secured_Sec,Delivery_Duration_Sec,Total_Turnaround_Sec");

            foreach (var set in session.sets)
            {
                foreach (var order in set.orders)
                {
                    string reqStr = EscapeCsv(string.Join("; ", order.requiredItems));
                    string asmStr = EscapeCsv(string.Join("; ", order.assembledItems));
                    string misStr = EscapeCsv(string.Join("; ", order.missingItems));
                    string extStr = EscapeCsv(string.Join("; ", order.extraItems));

                    sb.Append($"{EscapeCsv(session.sessionId)},");
                    sb.Append($"{EscapeCsv(session.subjectId)},");
                    sb.Append($"{EscapeCsv(order.workloadCondition)},");
                    sb.Append($"{order.setNumber},");
                    sb.Append($"{EscapeCsv(order.setName)},");
                    sb.Append($"{order.orderNumberInSet},");
                    sb.Append($"{order.overallOrderNumber},");
                    sb.Append($"{EscapeCsv(order.orderTitle)},");
                    sb.Append($"{order.calorieLimit},");
                    sb.Append($"{order.sugarLimit},");
                    sb.Append($"{order.fatLimit},");
                    sb.Append($"Table {order.targetTableNumber},");
                    sb.Append(order.selectedTableNumber > 0 ? $"Table {order.selectedTableNumber}," : "N/A,");
                    sb.Append($"{order.isTableMatch},");
                    sb.Append($"{reqStr},");
                    sb.Append($"{asmStr},");
                    sb.Append($"{order.isDishesMatch},");
                    sb.Append($"{order.isOrderFullyCorrect},");
                    sb.Append($"{misStr},");
                    sb.Append($"{extStr},");
                    sb.Append($"{order.wasTickedOnBoard},");
                    sb.Append($"{EscapeCsv(order.spawnTimestamp)},");
                    sb.Append($"{EscapeCsv(order.traySecuredTimestamp)},");
                    sb.Append($"{EscapeCsv(order.dispatchTimestamp)},");
                    sb.Append($"{EscapeCsv(order.deliveryCompleteTimestamp)},");
                    sb.Append($"{order.timeToDispatchSeconds.ToString("F2", CultureInfo.InvariantCulture)},");
                    sb.Append($"{order.timeToTraySecuredSeconds.ToString("F2", CultureInfo.InvariantCulture)},");
                    sb.Append($"{order.deliveryDurationSeconds.ToString("F2", CultureInfo.InvariantCulture)},");
                    sb.AppendLine($"{order.totalTurnaroundTimeSeconds.ToString("F2", CultureInfo.InvariantCulture)}");
                }
            }

            return sb.ToString();
        }

        public static string GenerateSetsCsv(TrialSessionRecord session)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Session_ID,Subject_ID,Set_Number,Set_Name,Start_Time,End_Time,Total_Duration_Sec,Total_Orders,Completed_Orders,Correct_Orders,Table_Accuracy_Pct,Dish_Accuracy_Pct,Overall_Accuracy_Pct,Avg_Time_To_Dispatch_Sec,Avg_Delivery_Duration_Sec,Avg_Turnaround_Sec");

            foreach (var set in session.sets)
            {
                sb.Append($"{EscapeCsv(session.sessionId)},");
                sb.Append($"{EscapeCsv(session.subjectId)},");
                sb.Append($"{set.setNumber},");
                sb.Append($"{EscapeCsv(set.setName)},");
                sb.Append($"{EscapeCsv(set.startTimestamp)},");
                sb.Append($"{EscapeCsv(set.endTimestamp)},");
                sb.Append($"{set.totalSetDurationSeconds.ToString("F2", CultureInfo.InvariantCulture)},");
                sb.Append($"{set.totalOrders},");
                sb.Append($"{set.completedOrders},");
                sb.Append($"{set.fullyCorrectOrders},");
                sb.Append($"{set.tableAccuracyPercent.ToString("F1", CultureInfo.InvariantCulture)},");
                sb.Append($"{set.dishAccuracyPercent.ToString("F1", CultureInfo.InvariantCulture)},");
                sb.Append($"{set.overallAccuracyPercent.ToString("F1", CultureInfo.InvariantCulture)},");
                sb.Append($"{set.avgTimeToDispatchSeconds.ToString("F2", CultureInfo.InvariantCulture)},");
                sb.Append($"{set.avgDeliveryDurationSeconds.ToString("F2", CultureInfo.InvariantCulture)},");
                sb.AppendLine($"{set.avgTurnaroundSeconds.ToString("F2", CultureInfo.InvariantCulture)}");
            }

            return sb.ToString();
        }

        public static string GenerateTrialSummaryCsv(TrialSessionRecord session)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Session_ID,Subject_ID,Session_Start,Session_End,Total_Sets_Configured,Completed_Sets_Count,Total_Orders_Dispatched,Total_Fully_Correct_Orders,Overall_Accuracy_Pct,Table_Accuracy_Pct,Dish_Accuracy_Pct,Total_Active_Trial_Duration_Sec,Total_Active_Trial_Duration_Formatted,Total_Wallclock_Duration_Sec,Overall_Avg_Time_To_Dispatch_Sec,Overall_Avg_Delivery_Duration_Sec,Overall_Avg_Turnaround_Sec");

            sb.Append($"{EscapeCsv(session.sessionId)},");
            sb.Append($"{EscapeCsv(session.subjectId)},");
            sb.Append($"{EscapeCsv(session.sessionStart)},");
            sb.Append($"{EscapeCsv(session.sessionEnd)},");
            sb.Append($"{session.totalSetsConfigured},");
            sb.Append($"{session.completedSetsCount},");
            sb.Append($"{session.totalOrdersDispatched},");
            sb.Append($"{session.totalFullyCorrectOrders},");
            sb.Append($"{session.overallAccuracyPercent.ToString("F1", CultureInfo.InvariantCulture)},");
            sb.Append($"{session.overallTableAccuracyPercent.ToString("F1", CultureInfo.InvariantCulture)},");
            sb.Append($"{session.overallDishAccuracyPercent.ToString("F1", CultureInfo.InvariantCulture)},");
            sb.Append($"{session.totalActiveTrialDurationSeconds.ToString("F2", CultureInfo.InvariantCulture)},");
            sb.Append($"{EscapeCsv(FormatDuration(session.totalActiveTrialDurationSeconds))},");
            sb.Append($"{session.totalWallClockDurationSeconds.ToString("F2", CultureInfo.InvariantCulture)},");
            sb.Append($"{session.overallAvgTimeToDispatchSeconds.ToString("F2", CultureInfo.InvariantCulture)},");
            sb.Append($"{session.overallAvgDeliveryDurationSeconds.ToString("F2", CultureInfo.InvariantCulture)},");
            sb.AppendLine($"{session.overallAvgTurnaroundSeconds.ToString("F2", CultureInfo.InvariantCulture)}");

            return sb.ToString();
        }

        public static string GenerateHtmlReport(TrialSessionRecord session)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html lang=\"en\">");
            sb.AppendLine("<head>");
            sb.AppendLine("    <meta charset=\"UTF-8\">");
            sb.AppendLine("    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
            sb.AppendLine($"    <title>VR Trial Report - {session.subjectId} ({session.sessionId})</title>");
            sb.AppendLine("    <style>");
            sb.AppendLine(@"
        :root {
            --primary: #2563eb;
            --primary-light: #dbeafe;
            --success: #16a34a;
            --success-light: #dcfce7;
            --danger: #dc2626;
            --danger-light: #fee2e2;
            --warning: #d97706;
            --warning-light: #fef3c7;
            --gray-50: #f8fafc;
            --gray-100: #f1f5f9;
            --gray-200: #e2e8f0;
            --gray-700: #334155;
            --gray-800: #1e293b;
            --gray-900: #0f172a;
        }
        * { box-sizing: border-box; margin: 0; padding: 0; }
        body {
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif;
            background-color: var(--gray-50);
            color: var(--gray-800);
            line-height: 1.5;
            padding: 30px 20px;
        }
        .container {
            max-width: 1200px;
            margin: 0 auto;
            background: #ffffff;
            border-radius: 12px;
            box-shadow: 0 4px 6px -1px rgba(0, 0, 0, 0.07), 0 2px 4px -2px rgba(0, 0, 0, 0.05);
            padding: 36px;
        }
        .header {
            display: flex;
            justify-content: space-between;
            align-items: flex-start;
            border-bottom: 2px solid var(--gray-200);
            padding-bottom: 24px;
            margin-bottom: 28px;
        }
        .header h1 {
            font-size: 28px;
            color: var(--gray-900);
            margin-bottom: 6px;
        }
        .header-meta {
            color: var(--gray-700);
            font-size: 14px;
        }
        .badge {
            display: inline-block;
            padding: 4px 12px;
            border-radius: 9999px;
            font-size: 12px;
            font-weight: 600;
            text-transform: uppercase;
            letter-spacing: 0.5px;
        }
        .badge-success { background: var(--success-light); color: var(--success); }
        .badge-danger { background: var(--danger-light); color: var(--danger); }
        .badge-warning { background: var(--warning-light); color: var(--warning); }
        .badge-primary { background: var(--primary-light); color: var(--primary); }

        .metrics-grid {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
            gap: 16px;
            margin-bottom: 32px;
        }
        .metric-card {
            background: var(--gray-50);
            border: 1px solid var(--gray-200);
            border-radius: 10px;
            padding: 18px;
            text-align: center;
        }
        .metric-label {
            font-size: 13px;
            font-weight: 600;
            color: var(--gray-700);
            margin-bottom: 6px;
            text-transform: uppercase;
        }
        .metric-value {
            font-size: 26px;
            font-weight: 700;
            color: var(--gray-900);
        }
        .metric-sub {
            font-size: 12px;
            color: #64748b;
            margin-top: 4px;
        }

        h2 {
            font-size: 20px;
            color: var(--gray-900);
            margin: 28px 0 16px 0;
            display: flex;
            align-items: center;
            gap: 10px;
        }
        table {
            width: 100%;
            border-collapse: collapse;
            font-size: 13px;
            margin-bottom: 24px;
        }
        th, td {
            padding: 12px 14px;
            text-align: left;
            border-bottom: 1px solid var(--gray-200);
        }
        th {
            background: var(--gray-100);
            font-weight: 600;
            color: var(--gray-700);
        }
        tr:hover { background-color: var(--gray-50); }

        .item-tag {
            display: inline-block;
            background: #e2e8f0;
            color: #334155;
            padding: 2px 6px;
            border-radius: 4px;
            font-size: 11px;
            margin: 2px 2px;
        }
        .item-tag-missing {
            background: #fee2e2;
            color: #b91c1c;
        }
        .item-tag-extra {
            background: #fef3c7;
            color: #b45309;
        }

        .footer {
            margin-top: 36px;
            padding-top: 20px;
            border-top: 1px solid var(--gray-200);
            font-size: 12px;
            color: #94a3b8;
            display: flex;
            justify-content: space-between;
        }
        .print-btn {
            background: var(--primary);
            color: white;
            border: none;
            padding: 8px 16px;
            border-radius: 6px;
            cursor: pointer;
            font-weight: 600;
            font-size: 13px;
        }
        .print-btn:hover { background: #1d4ed8; }

        @media print {
            body { padding: 0; background: white; }
            .container { box-shadow: none; padding: 0; }
            .print-btn { display: none; }
        }
");
            sb.AppendLine("    </style>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");
            sb.AppendLine("    <div class=\"container\">");

            // Header
            sb.AppendLine("        <div class=\"header\">");
            sb.AppendLine("            <div>");
            sb.AppendLine($"                <h1>VR Trial Report: {session.subjectId}</h1>");
            sb.AppendLine($"                <div class=\"header-meta\">Session ID: <b>{session.sessionId}</b> &bull; Started: {session.sessionStart} &bull; Finished: {session.sessionEnd}</div>");
            sb.AppendLine("            </div>");
            sb.AppendLine("            <div style=\"text-align: right;\">");
            sb.AppendLine($"                <span class=\"badge {(session.overallAccuracyPercent >= 80f ? "badge-success" : (session.overallAccuracyPercent >= 50f ? "badge-warning" : "badge-danger"))}\">Accuracy: {session.overallAccuracyPercent:F1}%</span>");
            sb.AppendLine("                <div style=\"margin-top: 8px;\"><button class=\"print-btn\" onclick=\"window.print()\">Print to PDF / Page</button></div>");
            sb.AppendLine("            </div>");
            sb.AppendLine("        </div>");

            // Metric Overview Cards
            sb.AppendLine("        <div class=\"metrics-grid\">");
            sb.AppendLine("            <div class=\"metric-card\">");
            sb.AppendLine("                <div class=\"metric-label\">Active Study Time</div>");
            sb.AppendLine($"                <div class=\"metric-value\">{FormatDuration(session.totalActiveTrialDurationSeconds)}</div>");
            sb.AppendLine($"                <div class=\"metric-sub\">Wall-clock: {FormatDuration(session.totalWallClockDurationSeconds)}</div>");
            sb.AppendLine("            </div>");
            sb.AppendLine("            <div class=\"metric-card\">");
            sb.AppendLine("                <div class=\"metric-label\">Sets Completed</div>");
            sb.AppendLine($"                <div class=\"metric-value\">{session.completedSetsCount} / {session.sets.Count}</div>");
            sb.AppendLine($"                <div class=\"metric-sub\">{session.sets.Count} configured sets</div>");
            sb.AppendLine("            </div>");
            sb.AppendLine("            <div class=\"metric-card\">");
            sb.AppendLine("                <div class=\"metric-label\">Total Orders</div>");
            sb.AppendLine($"                <div class=\"metric-value\">{session.totalOrdersDispatched}</div>");
            sb.AppendLine($"                <div class=\"metric-sub\">{session.totalFullyCorrectOrders} fully correct</div>");
            sb.AppendLine("            </div>");
            sb.AppendLine("            <div class=\"metric-card\">");
            sb.AppendLine("                <div class=\"metric-label\">Avg Prep / Dispatch</div>");
            sb.AppendLine($"                <div class=\"metric-value\">{session.overallAvgTimeToDispatchSeconds:F1}s</div>");
            sb.AppendLine("                <div class=\"metric-sub\">Order spawn &rarr; Robot dispatch</div>");
            sb.AppendLine("            </div>");
            sb.AppendLine("            <div class=\"metric-card\">");
            sb.AppendLine("                <div class=\"metric-label\">Avg Delivery Transit</div>");
            sb.AppendLine($"                <div class=\"metric-value\">{session.overallAvgDeliveryDurationSeconds:F1}s</div>");
            sb.AppendLine("                <div class=\"metric-sub\">Dispatch &rarr; Table placement</div>");
            sb.AppendLine("            </div>");
            sb.AppendLine("        </div>");

            // Set Breakdown Table
            sb.AppendLine("        <h2>Set Performance Breakdown</h2>");
            sb.AppendLine("        <table>");
            sb.AppendLine("            <thead>");
            sb.AppendLine("                <tr>");
            sb.AppendLine("                    <th>Set</th>");
            sb.AppendLine("                    <th>Set Name</th>");
            sb.AppendLine("                    <th>Total Time</th>");
            sb.AppendLine("                    <th>Orders</th>");
            sb.AppendLine("                    <th>Correct</th>");
            sb.AppendLine("                    <th>Table Acc.</th>");
            sb.AppendLine("                    <th>Dish Acc.</th>");
            sb.AppendLine("                    <th>Overall Acc.</th>");
            sb.AppendLine("                    <th>Avg Prep (s)</th>");
            sb.AppendLine("                    <th>Avg Delivery (s)</th>");
            sb.AppendLine("                </tr>");
            sb.AppendLine("            </thead>");
            sb.AppendLine("            <tbody>");

            foreach (var set in session.sets)
            {
                sb.AppendLine("                <tr>");
                sb.AppendLine($"                    <td><b>Set {set.setNumber}</b></td>");
                sb.AppendLine($"                    <td>{set.setName}</td>");
                sb.AppendLine($"                    <td><b>{FormatDuration(set.totalSetDurationSeconds)}</b> ({set.totalSetDurationSeconds:F1}s)</td>");
                sb.AppendLine($"                    <td>{set.completedOrders} / {set.totalOrders}</td>");
                sb.AppendLine($"                    <td>{set.fullyCorrectOrders}</td>");
                sb.AppendLine($"                    <td>{set.tableAccuracyPercent:F1}%</td>");
                sb.AppendLine($"                    <td>{set.dishAccuracyPercent:F1}%</td>");
                sb.AppendLine($"                    <td><span class=\"badge {(set.overallAccuracyPercent >= 80f ? "badge-success" : (set.overallAccuracyPercent >= 50f ? "badge-warning" : "badge-danger"))}\">{set.overallAccuracyPercent:F1}%</span></td>");
                sb.AppendLine($"                    <td>{set.avgTimeToDispatchSeconds:F1}s</td>");
                sb.AppendLine($"                    <td>{set.avgDeliveryDurationSeconds:F1}s</td>");
                sb.AppendLine("                </tr>");
            }
            sb.AppendLine("            </tbody>");
            sb.AppendLine("        </table>");

            // Detailed Order Logs
            sb.AppendLine("        <h2>Detailed Order Timeline & Accuracy</h2>");
            sb.AppendLine("        <table>");
            sb.AppendLine("            <thead>");
            sb.AppendLine("                <tr>");
            sb.AppendLine("                    <th>#</th>");
            sb.AppendLine("                    <th>Set</th>");
            sb.AppendLine("                    <th>Order Title</th>");
            sb.AppendLine("                    <th>Required Items</th>");
            sb.AppendLine("                    <th>Assembled on Tray</th>");
            sb.AppendLine("                    <th>Table Target / Placed</th>");
            sb.AppendLine("                    <th>Prep Time (Spawn &rarr; Dispatch)</th>");
            sb.AppendLine("                    <th>Delivery Transit</th>");
            sb.AppendLine("                    <th>Total Turnaround</th>");
            sb.AppendLine("                    <th>Result</th>");
            sb.AppendLine("                </tr>");
            sb.AppendLine("            </thead>");
            sb.AppendLine("            <tbody>");

            foreach (var set in session.sets)
            {
                foreach (var order in set.orders)
                {
                    string reqTags = string.Join(" ", order.requiredItems.Select(i => $"<span class=\"item-tag\">{i}</span>"));
                    string asmTags = string.Join(" ", order.assembledItems.Select(i => $"<span class=\"item-tag\">{i}</span>"));

                    if (order.missingItems.Count > 0)
                    {
                        asmTags += " " + string.Join(" ", order.missingItems.Select(m => $"<span class=\"item-tag item-tag-missing\" title=\"Missing\">-{m}</span>"));
                    }
                    if (order.extraItems.Count > 0)
                    {
                        asmTags += " " + string.Join(" ", order.extraItems.Select(e => $"<span class=\"item-tag item-tag-extra\" title=\"Extra / Incorrect\">+{e}</span>"));
                    }
                    if (order.assembledItems.Count == 0 && order.missingItems.Count == 0)
                    {
                        asmTags = "<span style=\"color:#94a3b8; font-style:italic;\">None</span>";
                    }

                    string tableInfo = $"Target: <b>T{order.targetTableNumber}</b> | Given: <b>{(order.selectedTableNumber > 0 ? "T" + order.selectedTableNumber : "N/A")}</b>";
                    string resultBadge;
                    if (order.isOrderFullyCorrect)
                    {
                        resultBadge = "<span class=\"badge badge-success\">Correct</span>";
                    }
                    else if (order.isTableMatch && !order.isDishesMatch)
                    {
                        resultBadge = "<span class=\"badge badge-warning\">Dish Mismatch</span>";
                    }
                    else if (!order.isTableMatch && order.isDishesMatch)
                    {
                        resultBadge = "<span class=\"badge badge-warning\">Table Mismatch</span>";
                    }
                    else
                    {
                        resultBadge = "<span class=\"badge badge-danger\">Incorrect</span>";
                    }

                    sb.AppendLine("                <tr>");
                    sb.AppendLine($"                    <td><b>{order.overallOrderNumber}</b></td>");
                    sb.AppendLine($"                    <td>Set {order.setNumber}</td>");
                    sb.AppendLine($"                    <td><b>{order.orderTitle}</b></td>");
                    sb.AppendLine($"                    <td>{reqTags}</td>");
                    sb.AppendLine($"                    <td>{asmTags}</td>");
                    sb.AppendLine($"                    <td>{tableInfo}</td>");
                    sb.AppendLine($"                    <td><b>{order.timeToDispatchSeconds:F1}s</b></td>");
                    sb.AppendLine($"                    <td>{order.deliveryDurationSeconds:F1}s</td>");
                    sb.AppendLine($"                    <td><b>{order.totalTurnaroundTimeSeconds:F1}s</b></td>");
                    sb.AppendLine($"                    <td>{resultBadge}</td>");
                    sb.AppendLine("                </tr>");
                }
            }

            sb.AppendLine("            </tbody>");
            sb.AppendLine("        </table>");

            // Footer
            sb.AppendLine("        <div class=\"footer\">");
            sb.AppendLine($"            <div>Cafe Social Robotics VR Trial Logging System &bull; Participant: {session.subjectId}</div>");
            sb.AppendLine($"            <div>Generated: {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}</div>");
            sb.AppendLine("        </div>");

            sb.AppendLine("    </div>");
            sb.AppendLine("</body>");
            sb.AppendLine("</html>");

            return sb.ToString();
        }

        public static string GenerateMarkdownReport(TrialSessionRecord session)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"# VR Study Trial Report: {session.subjectId}");
            sb.AppendLine();
            sb.AppendLine($"- **Session ID**: `{session.sessionId}`");
            sb.AppendLine($"- **Subject ID**: `{session.subjectId}`");
            sb.AppendLine($"- **Session Start**: {session.sessionStart}");
            sb.AppendLine($"- **Session End**: {session.sessionEnd}");
            sb.AppendLine($"- **Total Active Study Duration**: **{FormatDuration(session.totalActiveTrialDurationSeconds)}** ({session.totalActiveTrialDurationSeconds:F1}s)");
            sb.AppendLine($"- **Total Wall-Clock Duration**: **{FormatDuration(session.totalWallClockDurationSeconds)}**");
            sb.AppendLine($"- **Sets Completed**: {session.completedSetsCount} / {session.sets.Count}");
            sb.AppendLine($"- **Total Orders Dispatched**: {session.totalOrdersDispatched}");
            sb.AppendLine($"- **Overall Accuracy**: **{session.overallAccuracyPercent:F1}%** ({session.totalFullyCorrectOrders}/{session.totalOrdersDispatched} fully correct)");
            sb.AppendLine($"- **Table Accuracy**: {session.overallTableAccuracyPercent:F1}%");
            sb.AppendLine($"- **Dish Accuracy**: {session.overallDishAccuracyPercent:F1}%");
            sb.AppendLine($"- **Overall Avg Time to Dispatch (Prep Time)**: {session.overallAvgTimeToDispatchSeconds:F1}s");
            sb.AppendLine($"- **Overall Avg Delivery Transit**: {session.overallAvgDeliveryDurationSeconds:F1}s");
            sb.AppendLine($"- **Overall Avg Turnaround Time**: {session.overallAvgTurnaroundSeconds:F1}s");
            sb.AppendLine();

            sb.AppendLine("## Set Summary");
            sb.AppendLine();
            sb.AppendLine("| Set | Set Name | Total Duration | Orders | Correct | Table Acc. | Dish Acc. | Overall Acc. | Avg Prep (s) | Avg Delivery (s) |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|");

            foreach (var set in session.sets)
            {
                sb.AppendLine($"| Set {set.setNumber} | {set.setName} | {FormatDuration(set.totalSetDurationSeconds)} ({set.totalSetDurationSeconds:F1}s) | {set.completedOrders}/{set.totalOrders} | {set.fullyCorrectOrders} | {set.tableAccuracyPercent:F1}% | {set.dishAccuracyPercent:F1}% | **{set.overallAccuracyPercent:F1}%** | {set.avgTimeToDispatchSeconds:F1}s | {set.avgDeliveryDurationSeconds:F1}s |");
            }

            sb.AppendLine();
            sb.AppendLine("## Detailed Order Log");
            sb.AppendLine();
            sb.AppendLine("| # | Set | Title | Required Items | Assembled Items | Target Table | Selected Table | Prep Time (s) | Delivery (s) | Turnaround (s) | Table Match | Dish Match | Fully Correct | Ticked |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|");

            foreach (var set in session.sets)
            {
                foreach (var order in set.orders)
                {
                    string reqStr = string.Join("; ", order.requiredItems);
                    string asmStr = order.assembledItems.Count > 0 ? string.Join("; ", order.assembledItems) : "None";
                    string selTbl = order.selectedTableNumber > 0 ? $"Table {order.selectedTableNumber}" : "N/A";

                    sb.AppendLine($"| {order.overallOrderNumber} | Set {order.setNumber} | {order.orderTitle} | {reqStr} | {asmStr} | Table {order.targetTableNumber} | {selTbl} | {order.timeToDispatchSeconds:F1} | {order.deliveryDurationSeconds:F1} | {order.totalTurnaroundTimeSeconds:F1} | {(order.isTableMatch ? "Yes" : "No")} | {(order.isDishesMatch ? "Yes" : "No")} | **{(order.isOrderFullyCorrect ? "Yes" : "No")}** | {(order.wasTickedOnBoard ? "Yes" : "No")} |");
                }
            }

            return sb.ToString();
        }

        private static string EscapeCsv(string field)
        {
            if (string.IsNullOrEmpty(field)) return "\"\"";
            if (field.Contains(",") || field.Contains("\"") || field.Contains("\n") || field.Contains(";"))
            {
                return "\"" + field.Replace("\"", "\"\"") + "\"";
            }
            return "\"" + field + "\"";
        }

        private static string FormatDuration(float seconds)
        {
            if (seconds < 0f) seconds = 0f;
            int mins = (int)(seconds / 60f);
            float secs = seconds % 60f;
            if (mins > 0)
            {
                return $"{mins}m {secs:F1}s";
            }
            return $"{secs:F1}s";
        }
    }
}
