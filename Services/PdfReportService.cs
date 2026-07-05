using System.Globalization;
using System.Text;
using MediVault.Models;
using MediVault.ViewModels;

namespace MediVault.Services;

public class PdfReportService
{
    public byte[] BuildPatientRecordPdf(PatientRecordFormViewModel record, bool isRedacted)
    {
        var lines = new List<string>
        {
            "MediVault Health Center",
            "============================================================",
            isRedacted ? "Redacted Patient Record Report" : "Confidential Patient Record Report",
            $"Generated: {DateTime.Now:g}",
            $"Privacy Mode: {(isRedacted ? "Administrative redaction applied" : "Clinical authorized export")}",
            string.Empty,
            "PATIENT SUMMARY",
            "---------------",
            $"Patient: {record.FirstName} {record.LastName}",
            $"Record Reference: #{record.Id:D4}",
            $"Age: {record.Age}",
            $"Status: {record.Status}",
            $"Primary Diagnosis: {record.Diagnosis}",
            $"Date Admitted: {record.DateAdmitted:D}",
            string.Empty,
            "CLINICAL CARE PROGRESS HISTORY",
            "------------------------------",
            string.IsNullOrWhiteSpace(record.MedicalHistory) ? "No clinical notes registered." : record.MedicalHistory,
            string.Empty,
            "DETAILED DIAGNOSES",
            "------------------",
            string.IsNullOrWhiteSpace(record.Diagnoses) ? "No secondary diagnoses registered." : record.Diagnoses,
            string.Empty,
            "PRESCRIPTIONS",
            "-------------",
            string.IsNullOrWhiteSpace(record.Prescriptions) ? "No prescriptions active." : record.Prescriptions
        };

        return BuildSimplePdf(lines);
    }

    public byte[] BuildAuditLogPdf(IEnumerable<AuditLog> logs)
    {
        var lines = new List<string>
        {
            "MediVault Security Audit Report",
            "============================================================",
            $"Generated: {DateTime.Now:g}",
            $"Entries included: {logs.Count()}",
            string.Empty
        };

        foreach (var log in logs)
        {
            lines.Add($"{log.Timestamp:g} | {log.Action} | {log.EntityType}");
            lines.Add($"User Email: {log.UserEmail}");
            lines.Add($"User ID: {log.UserId}");
            lines.Add($"Details: {log.Description} {log.Details}");
            lines.Add($"IP: {log.IpAddress}");
            lines.Add(string.Empty);
        }

        if (lines.Count == 3)
        {
            lines.Add("No audit logs found for the selected filters.");
        }

        return BuildSimplePdf(lines);
    }

    public byte[] BuildInvoicePdf(BillingRecord bill)
    {
        var patientName = bill.PatientRecord?.PatientName ?? $"Patient record #{bill.PatientRecordId:D4}";
        var lines = new List<string>
        {
            "MediVault Billing Invoice",
            "============================================================",
            $"Invoice Reference: INV-{bill.Id:D6}",
            $"Generated: {DateTime.Now:g}",
            string.Empty,
            "PATIENT",
            "-------",
            $"Patient: {patientName}",
            $"Patient Record: #{bill.PatientRecordId:D4}",
            string.Empty,
            "BILLING DETAILS",
            "---------------",
            $"Description: {bill.Description}",
            $"Amount Due: {bill.Amount:C}",
            $"Status: {bill.Status}",
            $"Created: {bill.CreatedAt:g}",
            $"Created By User: {bill.CreatedByUserId}",
            string.Empty,
            "This invoice export is audit logged by MediVault."
        };

        return BuildSimplePdf(lines);
    }

    private static byte[] BuildSimplePdf(IReadOnlyList<string> lines)
    {
        var objects = new List<string>
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"
        };

        var content = new StringBuilder();
        content.AppendLine("BT");
        content.AppendLine("/F1 11 Tf");
        content.AppendLine("50 742 Td");

        var emitted = 0;
        foreach (var rawLine in lines.SelectMany(WrapLine))
        {
            if (emitted >= 48)
            {
                content.AppendLine($"({EscapePdfText("Report truncated. Export CSV for complete data.")}) Tj");
                break;
            }

            content.AppendLine($"({EscapePdfText(rawLine)}) Tj");
            content.AppendLine("0 -14 Td");
            emitted++;
        }

        content.AppendLine("ET");
        var contentBytes = Encoding.ASCII.GetBytes(content.ToString());
        objects.Add($"<< /Length {contentBytes.Length} >>\nstream\n{content}endstream");

        var pdf = new StringBuilder();
        pdf.AppendLine("%PDF-1.4");
        var offsets = new List<int> { 0 };

        for (var i = 0; i < objects.Count; i++)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(pdf.ToString()));
            pdf.AppendLine($"{i + 1} 0 obj");
            pdf.AppendLine(objects[i]);
            pdf.AppendLine("endobj");
        }

        var xrefOffset = Encoding.ASCII.GetByteCount(pdf.ToString());
        pdf.AppendLine("xref");
        pdf.AppendLine($"0 {objects.Count + 1}");
        pdf.AppendLine("0000000000 65535 f ");
        foreach (var offset in offsets.Skip(1))
        {
            pdf.AppendLine(offset.ToString("D10", CultureInfo.InvariantCulture) + " 00000 n ");
        }

        pdf.AppendLine("trailer");
        pdf.AppendLine($"<< /Size {objects.Count + 1} /Root 1 0 R >>");
        pdf.AppendLine("startxref");
        pdf.AppendLine(xrefOffset.ToString(CultureInfo.InvariantCulture));
        pdf.AppendLine("%%EOF");

        return Encoding.ASCII.GetBytes(pdf.ToString());
    }

    private static IEnumerable<string> WrapLine(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            yield return string.Empty;
            yield break;
        }

        var normalized = value.Replace("\r", string.Empty);
        foreach (var line in normalized.Split('\n'))
        {
            for (var i = 0; i < line.Length; i += 92)
            {
                yield return line.Substring(i, Math.Min(92, line.Length - i));
            }
        }
    }

    private static string EscapePdfText(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace("(", "\\(")
            .Replace(")", "\\)");
    }
}
