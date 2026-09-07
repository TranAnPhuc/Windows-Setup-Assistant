const fs = require('fs');
const {
  Document, Packer, Paragraph, TextRun, HeadingLevel, AlignmentType,
  Table, TableRow, TableCell, WidthType, ShadingType, BorderStyle,
  PageBreak, TableOfContents, LevelFormat, convertInchesToTwip
} = require('docx');

// ---------------------------------------------------------------- hằng số
const CONTENT_W = 9000;               // bề rộng vùng nội dung (DXA) cho khổ A4, lề 1 inch
const ACCENT = '1F4E79';
const ACCENT_LIGHT = 'DEEAF6';
const GREY_LIGHT = 'F2F2F2';
const OK = '2E7D32';
const WARN = 'B26A00';
const BAD = 'C00000';

// ---------------------------------------------------------------- helper
const P = (text, opts = {}) => new Paragraph({
  spacing: { after: opts.after ?? 120, line: 276 },
  keepNext: opts.keepNext,
  alignment: opts.align,
  indent: opts.indent,
  children: [new TextRun({
    text,
    bold: opts.bold,
    italics: opts.italics,
    size: opts.size,
    color: opts.color,
    font: opts.font
  })]
});

// Đoạn nhiều run (để in đậm một phần câu)
const PR = (runs, opts = {}) => new Paragraph({
  spacing: { after: opts.after ?? 120, line: 276 },
  indent: opts.indent,
  children: runs.map(r => typeof r === 'string'
    ? new TextRun({ text: r })
    : new TextRun(r))
});

const H1 = (text) => new Paragraph({
  heading: HeadingLevel.HEADING_1,
  spacing: { before: 360, after: 160 },
  keepNext: true,
  keepLines: true,
  children: [new TextRun({ text, bold: true, size: 30, color: ACCENT })]
});

const H2 = (text) => new Paragraph({
  heading: HeadingLevel.HEADING_2,
  spacing: { before: 260, after: 120 },
  keepNext: true,
  keepLines: true,
  children: [new TextRun({ text, bold: true, size: 25, color: ACCENT })]
});

const bullet = (text, level = 0) => new Paragraph({
  numbering: { reference: 'bullets', level },
  spacing: { after: 80, line: 276 },
  children: [new TextRun(text)]
});

const bulletR = (runs, level = 0) => new Paragraph({
  numbering: { reference: 'bullets', level },
  spacing: { after: 80, line: 276 },
  children: runs.map(r => typeof r === 'string' ? new TextRun({ text: r }) : new TextRun(r))
});

const step = (text, ref) => new Paragraph({
  numbering: { reference: ref, level: 0 },
  spacing: { after: 100, line: 276 },
  children: [new TextRun(text)]
});

const stepR = (runs, ref) => new Paragraph({
  numbering: { reference: ref, level: 0 },
  spacing: { after: 100, line: 276 },
  children: runs.map(r => typeof r === 'string' ? new TextRun({ text: r }) : new TextRun(r))
});

const code = (text) => new Paragraph({
  spacing: { before: 80, after: 140 },
  keepLines: true,
  shading: { type: ShadingType.CLEAR, fill: GREY_LIGHT },
  border: {
    top: { style: BorderStyle.SINGLE, size: 4, color: 'D0D0D0' },
    bottom: { style: BorderStyle.SINGLE, size: 4, color: 'D0D0D0' },
    left: { style: BorderStyle.SINGLE, size: 4, color: 'D0D0D0' },
    right: { style: BorderStyle.SINGLE, size: 4, color: 'D0D0D0' }
  },
  indent: { left: 120, right: 120 },
  children: [new TextRun({ text, font: 'Consolas', size: 18 })]
});

// Hộp ghi chú / cảnh báo
const callout = (label, text, color) => new Paragraph({
  spacing: { before: 140, after: 160 },
  keepLines: true,
  shading: { type: ShadingType.CLEAR, fill: 'FBFBFB' },
  border: { left: { style: BorderStyle.SINGLE, size: 18, color } },
  indent: { left: 160, right: 120 },
  children: [
    new TextRun({ text: label + '  ', bold: true, color }),
    new TextRun({ text })
  ]
});

const cell = (text, opts = {}) => new TableCell({
  width: { size: opts.width, type: WidthType.DXA },
  shading: opts.fill ? { type: ShadingType.CLEAR, fill: opts.fill } : undefined,
  margins: { top: 80, bottom: 80, left: 120, right: 120 },
  children: (Array.isArray(text) ? text : [text]).map(t => new Paragraph({
    spacing: { after: 0, line: 260 },
    children: [new TextRun({
      text: t,
      bold: opts.bold,
      color: opts.color,
      font: opts.mono ? 'Consolas' : undefined,
      size: opts.mono ? 18 : 20
    })]
  }))
});

/**
 * table(columnWidths, headerRow, dataRows, options)
 * dataRows: mảng các mảng; mỗi ô có thể là chuỗi hoặc { t, mono, bold, color }
 */
const table = (widths, header, rows, opts = {}) => new Table({
  width: { size: widths.reduce((a, b) => a + b, 0), type: WidthType.DXA },
  columnWidths: widths,
  borders: {
    top: { style: BorderStyle.SINGLE, size: 4, color: 'BFBFBF' },
    bottom: { style: BorderStyle.SINGLE, size: 4, color: 'BFBFBF' },
    left: { style: BorderStyle.SINGLE, size: 4, color: 'BFBFBF' },
    right: { style: BorderStyle.SINGLE, size: 4, color: 'BFBFBF' },
    insideHorizontal: { style: BorderStyle.SINGLE, size: 4, color: 'D9D9D9' },
    insideVertical: { style: BorderStyle.SINGLE, size: 4, color: 'D9D9D9' }
  },
  rows: [
    new TableRow({
      tableHeader: true,
      cantSplit: true,
      children: header.map((h, i) => cell(h, { width: widths[i], bold: true, fill: ACCENT_LIGHT }))
    }),
    ...rows.map((r, ri) => new TableRow({
      cantSplit: true,
      children: r.map((c, i) => {
        const o = typeof c === 'object' ? c : { t: c };
        return cell(o.t, {
          width: widths[i],
          mono: o.mono,
          bold: o.bold,
          color: o.color,
          fill: opts.zebra && ri % 2 === 1 ? 'FAFAFA' : undefined
        });
      })
    }))
  ]
});

const spacer = (h = 200) => new Paragraph({ spacing: { after: h }, children: [] });

// ================================================================ NỘI DUNG
const children = [];

// ---------- Trang bìa ----------
children.push(
  new Paragraph({ spacing: { before: 1800, after: 0 }, children: [] }),
  new Paragraph({
    alignment: AlignmentType.CENTER,
    spacing: { after: 120 },
    children: [new TextRun({ text: 'HƯỚNG DẪN SỬ DỤNG', bold: true, size: 34, color: '595959' })]
  }),
  new Paragraph({
    alignment: AlignmentType.CENTER,
    spacing: { after: 200 },
    children: [new TextRun({ text: 'WINDOWS SETUP ASSISTANT', bold: true, size: 56, color: ACCENT })]
  }),
  new Paragraph({
    alignment: AlignmentType.CENTER,
    spacing: { after: 900 },
    border: { top: { style: BorderStyle.SINGLE, size: 8, color: ACCENT } },
    children: [new TextRun({
      text: 'Công cụ cài đặt hàng loạt phần mềm bằng WinGet',
      size: 24, color: '595959'
    })]
  })
);

children.push(table(
  [2600, 6400],
  ['Hạng mục', 'Nội dung'],
  [
    ['Đối tượng sử dụng', 'Kỹ thuật viên IT, nhân viên IT Helpdesk'],
    ['Mục đích', 'Rút ngắn thời gian cài đặt phần mềm cho máy mới hoặc máy vừa cài lại Windows'],
    ['Phiên bản ứng dụng', '1.0.1'],
    ['Hệ điều hành hỗ trợ', 'Windows 10 (1809 trở lên) và Windows 11, bản 64-bit'],
    ['Yêu cầu kèm theo', 'WinGet (nằm trong ứng dụng App Installer của Microsoft Store)'],
    ['Cài .NET trên máy đích', 'KHÔNG cần — bản phát hành đã nhúng sẵn .NET runtime'],
    ['Quyền cần thiết', 'Chạy bằng quyền người dùng thường; chỉ nâng quyền khi thật sự cần'],
    ['Ngày cập nhật', '07/09/2026']
  ],
  { zebra: true }
));

children.push(new Paragraph({ children: [new PageBreak()] }));

// ---------- Mục lục ----------
children.push(
  H1('Mục lục'),
  new TableOfContents('Mục lục', { hyperlink: true, headingStyleRange: '1-2' }),
  spacer(200),
  callout('Lưu ý:', 'Nếu bạn chỉnh sửa tài liệu này, hãy nhấn Ctrl+A rồi F9 và chọn "Update entire table" để Word đánh lại số trang cho mục lục.', WARN),
  new Paragraph({ children: [new PageBreak()] })
);

// ---------- 1. Tổng quan ----------
children.push(
  H1('1. Tổng quan'),
  P('Windows Setup Assistant là công cụ giúp kỹ thuật viên cài nhiều phần mềm cùng lúc chỉ bằng một thao tác, thay vì phải mở trình duyệt, tìm trang tải, tải file cài đặt rồi bấm "Next" cho từng phần mềm một.'),
  P('Công cụ không tự tải file EXE/MSI từ bất kỳ website nào. Toàn bộ việc tải và cài do WinGet — trình quản lý gói chính thức của Microsoft — thực hiện từ kho phần mềm đã được kiểm duyệt.'),

  H2('1.1. So sánh quy trình'),
  table(
    [1200, 3900, 3900],
    ['Bước', 'Cách làm thủ công (cho MỖI phần mềm)', 'Dùng Windows Setup Assistant'],
    [
      ['1', 'Mở trình duyệt, tìm tên phần mềm', 'Cắm USB, mở công cụ'],
      ['2', 'Chọn đúng trang chính chủ (dễ nhầm trang giả)', 'Chọn cấu hình phù hợp với máy'],
      ['3', 'Tải file cài đặt về', 'Tích chọn các phần mềm cần cài'],
      ['4', 'Chạy file, bấm Next nhiều lần', 'Bấm "Bắt đầu cài đặt" một lần duy nhất'],
      ['5', 'Bỏ tick các phần mềm rác đi kèm', 'Đi làm việc khác, quay lại xem kết quả'],
      ['6', 'Lặp lại toàn bộ cho phần mềm tiếp theo', '(không cần lặp lại)']
    ],
    { zebra: true }
  ),
  spacer(160),
  P('Với một máy cần 10 phần mềm, cách thủ công phải lặp 6 bước trên 10 lần và kỹ thuật viên phải ngồi canh máy. Với công cụ này, số thao tác không tăng theo số phần mềm — chọn 10 phần mềm hay 30 phần mềm cũng chỉ bấm một nút.'),

  H2('1.2. Điểm mạnh khi dùng trong môi trường doanh nghiệp'),
  bullet('Chuẩn hoá: mọi máy cùng phòng ban được cài đúng cùng một bộ phần mềm, không phụ thuộc trí nhớ của từng kỹ thuật viên.'),
  bullet('Mang đi được: chép cả thư mục vào USB là dùng được ngay trên máy mới, không cần cài đặt gì trước.'),
  bullet('Không mất dữ liệu: danh sách phần mềm nằm trong file JSON đi kèm, mang sang máy khác vẫn còn nguyên.'),
  bullet('Chịu lỗi tốt: một phần mềm cài lỗi không làm dừng cả hàng đợi; các phần mềm còn lại vẫn được cài tiếp.'),
  bullet('Có nhật ký: mọi câu lệnh, thời điểm, mã lỗi đều được ghi lại để đối chiếu khi cần hỗ trợ.'),

  new Paragraph({ children: [new PageBreak()] })
);

// ---------- 2. Chuẩn bị ----------
children.push(
  H1('2. Chuẩn bị bộ công cụ (làm một lần duy nhất)'),
  P('Bộ công cụ gồm một file chương trình và một thư mục dữ liệu. Cả hai phải nằm cạnh nhau.'),
  code(
    'USB hoặc thư mục dùng chung/\n' +
    '└─ WindowsSetupAssistant/\n' +
    '   ├─ WindowsSetupAssistant.exe     ← chương trình (khoảng 63 MB)\n' +
    '   ├─ Data/\n' +
    '   │  ├─ software-list.json         ← DANH SÁCH PHẦN MỀM (quan trọng nhất)\n' +
    '   │  └─ app-settings.json          ← tuỳ chọn giao diện Sáng/Tối\n' +
    '   └─ Logs/\n' +
    '      └─ setup-assistant-YYYYMMDD.log'
  ),
  callout('Quan trọng:',
    'Phải chép NGUYÊN CẢ THƯ MỤC. Nếu chỉ chép mỗi file .exe thì danh sách phần mềm của đơn vị sẽ mất và công cụ tạo lại danh sách mẫu mặc định.', BAD),
  P('Thư mục Data và Logs sẽ tự sinh ra ở lần chạy đầu tiên. Sau khi kỹ thuật viên phụ trách đã dựng xong danh sách chuẩn, hãy chép cả thư mục này ra USB cho từng người trong đội.'),

  H2('2.1. Kiểm tra máy đích trước khi bắt đầu'),
  table(
    [3000, 3000, 3000],
    ['Cần kiểm tra', 'Cách kiểm tra', 'Nếu chưa đạt'],
    [
      ['Windows 64-bit', 'Nhấn phím Windows + Pause, xem mục "System type"', 'Công cụ không chạy được trên bản 32-bit'],
      ['Có WinGet', 'Mở Terminal, gõ: winget --version', 'Xem mục 3.4 của tài liệu này'],
      ['Có mạng Internet', 'Mở một trang web bất kỳ', 'Bắt buộc phải có mạng thì WinGet mới tải được'],
      ['Còn dung lượng ổ C', 'This PC → xem ổ C', 'Dọn bớt trước khi cài hàng loạt']
    ],
    { zebra: true }
  ),
  new Paragraph({ children: [new PageBreak()] })
);

// ---------- 3. Mở phần mềm đúng cách ----------
children.push(
  H1('3. Mở phần mềm đúng cách'),
  P('Phần lớn sự cố khi dùng công cụ đến từ việc mở sai cách. Hãy làm đúng theo thứ tự sau.'),

  H2('3.1. Bốn bước mở phần mềm'),
  stepR([
    'Cắm USB vào máy. ',
    { text: 'Nên chép cả thư mục WindowsSetupAssistant từ USB vào ổ đĩa của máy', bold: true },
    ' (ví dụ vào D:\\Tools\\). Chạy trực tiếp từ USB vẫn được, nhưng chạy từ ổ cứng sẽ nhanh hơn và tránh rủi ro rút nhầm USB giữa chừng.'
  ], 'steps-open'),
  stepR([
    { text: 'Không giải nén kiểu "mở trong file ZIP".', bold: true },
    ' Nếu nhận bộ công cụ ở dạng file nén, phải giải nén ra thư mục thật rồi mới chạy. Chạy thẳng từ bên trong cửa sổ ZIP sẽ khiến công cụ không lưu được danh sách.'
  ], 'steps-open'),
  stepR([
    'Nháy đúp chuột vào ',
    { text: 'WindowsSetupAssistant.exe', bold: true, font: 'Consolas' },
    '.'
  ], 'steps-open'),
  stepR([
    'Nếu Windows hiện cảnh báo màu xanh ',
    { text: '"Windows protected your PC"', italics: true },
    ' — xem mục 3.2 ngay bên dưới.'
  ], 'steps-open'),

  H2('3.2. Xử lý cảnh báo SmartScreen'),
  P('Windows hiển thị cảnh báo này với mọi file .exe chưa mua chứng thư số, kể cả phần mềm nội bộ hoàn toàn sạch. Đây là hiện tượng bình thường với công cụ tự phát triển.'),
  step('Bấm dòng chữ "More info" (Thông tin thêm).', 'steps-smart'),
  step('Kiểm tra tên file hiện ra đúng là WindowsSetupAssistant.exe.', 'steps-smart'),
  step('Bấm nút "Run anyway" (Vẫn chạy).', 'steps-smart'),
  callout('Cảnh báo:',
    'Chỉ bấm "Run anyway" khi file được lấy từ bộ công cụ chính thức của bộ phận IT. Tuyệt đối không làm thao tác này với file .exe nhận từ email hay tải trên mạng.', BAD),

  H2('3.3. Khi nào cần quyền Administrator'),
  P('Mặc định công cụ chạy bằng quyền người dùng thường — đây là lựa chọn đúng cho hầu hết trường hợp và an toàn hơn.'),
  P('Chỉ nâng quyền khi kết quả cài đặt báo lỗi về quyền (xem bảng mã lỗi ở mục 11). Khi đó:'),
  step('Bấm nút "Chạy bằng quyền Admin" ở góc dưới bên phải cửa sổ.', 'steps-admin'),
  step('Bấm "Yes" ở hộp thoại UAC của Windows.', 'steps-admin'),
  step('Ứng dụng tự đóng và mở lại; kiểm tra dòng chữ ở thanh dưới cùng đã đổi thành "Đang chạy với quyền Administrator".', 'steps-admin'),
  callout('Lưu ý:',
    'Một số trình cài đặt (ví dụ Visual Studio Code bản User) lại KHÔNG cho phép chạy dưới quyền Administrator. Nếu gặp lỗi loại này, hãy đóng và mở lại công cụ bằng quyền người dùng thường.', WARN),

  H2('3.4. Nếu máy chưa có WinGet'),
  P('Ngay khi mở, công cụ tự kiểm tra WinGet. Nếu thiếu, một khung cảnh báo màu cam hiện ra ở đầu cửa sổ và nút "Bắt đầu cài đặt" bị khoá.'),
  step('Bấm nút "Mở Microsoft Store" trên khung cảnh báo.', 'steps-winget'),
  step('Trong Microsoft Store, cài ứng dụng có tên "App Installer" (Trình cài đặt ứng dụng) của Microsoft.', 'steps-winget'),
  step('Đóng hẳn Windows Setup Assistant rồi mở lại.', 'steps-winget'),
  step('Kiểm tra khung cảnh báo đã biến mất và thanh trạng thái báo phiên bản WinGet.', 'steps-winget'),
  callout('Nếu máy không vào được Microsoft Store:',
    'Liên hệ quản trị hệ thống. Không tải file cài WinGet từ các trang web lạ.', WARN),

  H2('3.5. Dấu hiệu đã mở đúng'),
  table(
    [4500, 4500],
    ['Kiểm tra', 'Kết quả mong đợi'],
    [
      ['Khung cảnh báo màu cam ở đầu cửa sổ', 'Không xuất hiện'],
      ['Thanh trạng thái dưới cùng', 'Hiện đường dẫn tới file software-list.json'],
      ['Tab "Danh sách phần mềm"', 'Có sẵn danh sách, không trống trơn'],
      ['Cột "Trạng thái"', 'Sau 5–20 giây chuyển thành "Đã cài" hoặc "Chưa cài"'],
      ['Nút "Bắt đầu cài đặt"', 'Bấm được (không bị mờ)']
    ],
    { zebra: true }
  ),
  new Paragraph({ children: [new PageBreak()] })
);

// ---------- 4. Giao diện ----------
children.push(
  H1('4. Làm quen màn hình chính'),
  P('Cửa sổ chia thành bốn khu vực từ trên xuống dưới.'),
  table(
    [1000, 2600, 5400],
    ['Vị trí', 'Khu vực', 'Dùng để làm gì'],
    [
      ['Trên cùng', 'Thanh tiêu đề', 'Chọn cấu hình cài đặt; các nút Mới / Đổi tên / Nhân bản / Xoá cấu hình; nút đổi giao diện Sáng ↔ Tối'],
      ['Dưới đó', 'Khung cảnh báo', 'Chỉ hiện khi máy chưa có WinGet'],
      ['Giữa', 'Bốn thẻ (tab)', 'Danh sách phần mềm · Tìm trên WinGet · Kết quả cài đặt · Nhật ký'],
      ['Dưới cùng', 'Thanh trạng thái', 'Đường dẫn file dữ liệu; quyền đang chạy; các nút Mở thư mục dữ liệu, Nhập JSON, Xuất tất cả, Xuất cấu hình này']
    ],
    { zebra: true }
  ),

  H2('4.1. Bốn thẻ chức năng'),
  table(
    [2400, 6600],
    ['Thẻ', 'Nội dung'],
    [
      ['Danh sách phần mềm', 'Nơi làm việc chính: xem, thêm, sửa, xoá, sắp xếp, tích chọn và bấm cài đặt'],
      ['Tìm trên WinGet', 'Tra cứu phần mềm trong kho WinGet và thêm vào danh sách'],
      ['Kết quả cài đặt', 'Bảng kết quả của lượt cài vừa chạy: thành công, bỏ qua, thất bại, mã lỗi, thời gian'],
      ['Nhật ký', 'Toàn bộ câu lệnh đã chạy kèm thời điểm và mã lỗi; có nút sao chép và mở thư mục log']
    ],
    { zebra: true }
  ),

  new Paragraph({ children: [new PageBreak()] }),
  H2('4.2. Các cột trong bảng danh sách phần mềm'),
  table(
    [2000, 7000],
    ['Cột', 'Ý nghĩa'],
    [
      ['Cài', 'Ô tích chọn — chỉ những dòng được tích mới nằm trong lượt cài'],
      ['Tên phần mềm', 'Tên hiển thị, do người dùng tự đặt cho dễ đọc'],
      ['WinGet Package Id', 'Mã định danh chính xác của gói. Đây mới là thứ WinGet dùng để cài'],
      ['Nhóm', 'Trình duyệt, Lập trình, Văn phòng, Giải trí, Tiện ích, Khác'],
      ['Trạng thái', 'Đã cài / Chưa cài / Đang kiểm tra / kết quả lượt cài gần nhất'],
      ['Ghi chú', 'Ghi chú nội bộ, ví dụ "chỉ cài cho phòng Kế toán"']
    ],
    { zebra: true }
  ),

  H2('4.3. Màu của cột Trạng thái'),
  table(
    [2600, 6400],
    ['Màu chữ', 'Ý nghĩa'],
    [
      [{ t: 'Xanh lá', color: OK, bold: true }, 'Đã có trên máy, hoặc vừa cài/nâng cấp thành công'],
      [{ t: 'Xám', color: '767676', bold: true }, 'Chưa cài, hoặc đã bỏ qua vì có sẵn'],
      [{ t: 'Cam', color: WARN, bold: true }, 'Bị huỷ giữa chừng'],
      [{ t: 'Đỏ', color: BAD, bold: true }, 'Cài thất bại — cần xem thẻ Nhật ký']
    ],
    { zebra: true }
  ),
  new Paragraph({ children: [new PageBreak()] })
);

// ---------- 5. Quy trình chuẩn ----------
children.push(
  H1('5. Quy trình chuẩn cho một máy mới'),
  P('Đây là quy trình rút gọn dùng hằng ngày. Chi tiết từng thao tác nằm ở các mục sau.'),
  step('Chép thư mục WindowsSetupAssistant từ USB vào máy, mở file WindowsSetupAssistant.exe.', 'steps-flow'),
  step('Kiểm tra không có khung cảnh báo màu cam (nghĩa là WinGet đã sẵn sàng).', 'steps-flow'),
  step('Ở góc trên bên phải, chọn cấu hình phù hợp với máy đang cài (ví dụ "Máy văn phòng").', 'steps-flow'),
  step('Bấm "Kiểm tra đã cài" và chờ cột Trạng thái cập nhật xong.', 'steps-flow'),
  step('Bấm "Chỉ cái chưa cài" để tự động tích chọn đúng những phần mềm còn thiếu.', 'steps-flow'),
  step('Xem lại danh sách đã tích, bỏ tick những phần mềm máy này không cần.', 'steps-flow'),
  step('Bấm "Bắt đầu cài đặt", đọc kỹ hộp xác nhận rồi bấm nút xác nhận.', 'steps-flow'),
  step('Chờ chạy xong, đọc bảng tổng kết. Nếu có mục thất bại, bấm "Thử lại phần lỗi".', 'steps-flow'),
  step('Mở thẻ "Kết quả cài đặt" chụp màn hình lưu vào phiếu bàn giao máy (nếu quy trình đơn vị yêu cầu).', 'steps-flow'),
  callout('Mẹo tiết kiệm thời gian:',
    'Trong lúc công cụ chạy, kỹ thuật viên có thể làm việc khác trên cùng máy đó (đặt tên máy, join domain, cấu hình máy in). Cửa sổ công cụ không bị treo khi đang cài.', OK),
  new Paragraph({ children: [new PageBreak()] })
);

// ---------- 6. Cấu hình ----------
children.push(
  H1('6. Quản lý cấu hình cài đặt'),
  P('Cấu hình (profile) là một bộ phần mềm được đặt tên sẵn. Mỗi cấu hình có danh sách phần mềm riêng, giúp không phải tích chọn lại từ đầu cho từng loại máy.'),
  P('Bộ mặc định có sẵn ba cấu hình: "Máy cá nhân", "Máy lập trình", "Máy công ty". Đơn vị nên xây dựng lại theo phòng ban thực tế, ví dụ:'),
  bullet('Máy văn phòng — Kế toán'),
  bullet('Máy văn phòng — Kinh doanh'),
  bullet('Máy kỹ thuật / lập trình'),
  bullet('Máy phòng họp / máy dùng chung'),
  spacer(120),
  table(
    [2200, 6800],
    ['Nút', 'Công dụng'],
    [
      ['Mới', 'Tạo cấu hình rỗng, sau đó tự thêm phần mềm vào'],
      ['Đổi tên', 'Đổi tên cấu hình đang chọn'],
      ['Nhân bản', 'Tạo bản sao của cấu hình đang chọn — cách nhanh nhất để tạo cấu hình mới gần giống cái cũ'],
      ['Xoá', 'Xoá cấu hình đang chọn cùng toàn bộ phần mềm bên trong (có hỏi xác nhận)']
    ],
    { zebra: true }
  ),
  callout('Khuyến nghị:',
    'Khi cần một cấu hình mới gần giống cấu hình cũ, hãy dùng "Nhân bản" rồi chỉnh sửa, thay vì tạo mới và nhập lại từ đầu.', OK),
  callout('Lưu ý:', 'Không thể xoá cấu hình cuối cùng — hệ thống luôn giữ lại ít nhất một cấu hình.', WARN),
  new Paragraph({ children: [new PageBreak()] })
);

// ---------- 7. Thêm phần mềm ----------
children.push(
  H1('7. Thêm phần mềm mới vào danh sách'),
  P('Có hai cách thêm. Cách A an toàn hơn vì Package Id được lấy trực tiếp từ kho WinGet nên không thể gõ sai.'),

  H2('7.1. Cách A — Tìm trên WinGet (khuyến nghị)'),
  step('Mở thẻ "Tìm trên WinGet".', 'steps-addA'),
  step('Gõ từ khoá vào ô tìm kiếm, ví dụ: zalo, foxit, teamviewer. Nên gõ tiếng Anh không dấu.', 'steps-addA'),
  step('Bấm nút "Tìm kiếm" hoặc nhấn phím Enter, chờ vài giây.', 'steps-addA'),
  stepR([
    'Đối chiếu kết quả: xem cả cột ',
    { text: 'Tên phần mềm', bold: true },
    ' và cột ',
    { text: 'WinGet Package Id', bold: true },
    ' để chắc chắn đúng phần mềm cần cài (xem mục 7.4 về cách chọn đúng biến thể).'
  ], 'steps-addA'),
  step('Bấm nút "Thêm vào danh sách" ở cuối dòng tương ứng.', 'steps-addA'),
  step('Hộp thoại hiện ra với tên và Package Id đã điền sẵn. Chọn lại Nhóm phần mềm cho đúng, thêm Ghi chú nếu cần.', 'steps-addA'),
  step('Bấm "Lưu". Phần mềm xuất hiện ở cuối danh sách và được tự động lưu xuống file ngay lập tức.', 'steps-addA'),
  callout('Dấu hiệu nhận biết:',
    'Dòng nào đã có trong danh sách sẽ hiện chữ "Đã có trong danh sách" thay cho nút Thêm, nên không sợ thêm trùng.', OK),

  H2('7.2. Cách B — Nhập tay Package Id'),
  P('Dùng khi đã biết chắc Package Id, hoặc khi máy đang không có mạng để tìm kiếm.'),
  step('Mở thẻ "Danh sách phần mềm", bấm nút "Thêm".', 'steps-addB'),
  step('Nhập Tên phần mềm — đây là tên hiển thị, đặt sao cho đồng nghiệp dễ đọc.', 'steps-addB'),
  step('Nhập WinGet Package Id — phải chính xác tuyệt đối, phân biệt dấu chấm và dấu gạch ngang.', 'steps-addB'),
  step('Chọn Nhóm phần mềm.', 'steps-addB'),
  step('Nhập Ghi chú nếu cần, rồi bấm "Lưu".', 'steps-addB'),
  P('Nếu Package Id không hợp lệ, hộp thoại hiện khung đỏ báo lỗi và không cho lưu. Đây là cơ chế bảo vệ, không phải lỗi phần mềm.'),

  H2('7.3. Quy tắc Package Id hợp lệ'),
  table(
    [4500, 4500],
    ['Hợp lệ', 'Không hợp lệ'],
    [
      [{ t: 'Google.Chrome', mono: true, color: OK }, { t: 'Google Chrome  (có khoảng trắng)', mono: true, color: BAD }],
      [{ t: '7zip.7zip', mono: true, color: OK }, { t: '--force  (bắt đầu bằng dấu gạch)', mono: true, color: BAD }],
      [{ t: 'Notepad++.Notepad++', mono: true, color: OK }, { t: '.Google.Chrome  (bắt đầu bằng dấu chấm)', mono: true, color: BAD }],
      [{ t: 'VNGCorp.Zalo', mono: true, color: OK }, { t: '"Google.Chrome"  (có dấu nháy)', mono: true, color: BAD }],
      [{ t: 'Microsoft.VCRedist.2015+.x64', mono: true, color: OK }, { t: 'chrome & calc  (có ký tự lệnh)', mono: true, color: BAD }]
    ],
    { zebra: true }
  ),
  spacer(120),
  P('Tóm tắt: Package Id chỉ gồm chữ, số và các ký tự chấm, gạch dưới, gạch ngang, dấu cộng; phải bắt đầu bằng chữ hoặc số; tuyệt đối không có khoảng trắng.'),

  H2('7.4. Chọn đúng biến thể của gói'),
  P('Một phần mềm thường có nhiều gói trên WinGet. Chọn sai sẽ cài nhầm bản beta hoặc bản không phù hợp.'),
  table(
    [3000, 3000, 3000],
    ['Trường hợp', 'Nên chọn', 'Lý do'],
    [
      ['Google.Chrome và Google.Chrome.EXE', 'Google.Chrome', 'Bản chuẩn; đuôi .EXE là bản đóng gói khác'],
      ['Có kèm Beta / Dev / Canary / Nightly / PreRelease', 'Bản không có hậu tố', 'Bản thử nghiệm không dùng cho máy nhân viên'],
      ['Adobe.Acrobat.Reader.64-bit và .32-bit', 'Bản 64-bit', 'Máy công ty hiện nay đều là 64-bit'],
      ['Foxit.FoxitReader và Foxit.FoxitReader.Inno', 'Foxit.FoxitReader', 'Bản đóng gói chính thức'],
      ['OpenJS.NodeJS và OpenJS.NodeJS.20', 'Tuỳ yêu cầu dự án', 'Bản có số là bản cố định phiên bản']
    ],
    { zebra: true }
  ),

  H2('7.5. Tra Package Id bằng dòng lệnh (khi cần đối chiếu)'),
  P('Mở Terminal hoặc Command Prompt trên máy và gõ:'),
  code('winget search "tên phần mềm" --source winget'),
  P('Cột "Id" trong kết quả chính là WinGet Package Id cần nhập vào công cụ.'),
  new Paragraph({ children: [new PageBreak()] })
);

// ---------- 8. Sửa xoá sắp xếp ----------
children.push(
  H1('8. Sửa, xoá và sắp xếp phần mềm'),
  H2('8.1. Sửa thông tin'),
  step('Bấm chọn dòng cần sửa trong bảng.', 'steps-edit'),
  step('Bấm nút "Sửa" — hoặc nháy đúp chuột thẳng vào dòng đó.', 'steps-edit'),
  step('Chỉnh sửa rồi bấm "Lưu".', 'steps-edit'),

  H2('8.2. Xoá khỏi danh sách'),
  step('Bấm chọn dòng cần xoá.', 'steps-del'),
  step('Bấm nút "Xoá" — hoặc nhấn phím Delete.', 'steps-del'),
  step('Xác nhận trong hộp thoại.', 'steps-del'),
  callout('Cần phân biệt:',
    'Thao tác này chỉ xoá phần mềm khỏi DANH SÁCH của công cụ. Nó KHÔNG gỡ cài đặt phần mềm đang có trên máy.', WARN),

  H2('8.3. Sắp xếp thứ tự cài'),
  P('Chọn một dòng rồi dùng nút mũi tên lên (↑) và mũi tên xuống (↓) để di chuyển. Phần mềm được cài lần lượt từ trên xuống dưới đúng theo thứ tự này.'),
  P('Nên xếp theo nguyên tắc sau:'),
  bullet('Đưa phần mềm quan trọng nhất lên đầu (trình duyệt, phần mềm nghiệp vụ) để nếu phải dừng giữa chừng thì máy vẫn dùng được việc.'),
  bullet('Đưa phần mềm nặng, tải lâu (bộ Office, IDE lập trình) xuống cuối.'),
  bullet('Đưa các thành phần phụ thuộc (runtime, thư viện) lên trước phần mềm cần chúng.'),

  H2('8.4. Lọc và tìm nhanh trong danh sách'),
  P('Khi danh sách dài, dùng ô tìm kiếm ở góc trên bên trái thẻ "Danh sách phần mềm" — gõ được cả tên lẫn Package Id. Ô chọn nhóm bên cạnh giúp lọc theo nhóm phần mềm.'),
  callout('Cần nhớ:',
    'Các nút chọn nhanh (Chọn tất cả, Bỏ chọn, Đảo chọn, Chỉ cái chưa cài) chỉ tác động lên những dòng ĐANG HIỂN THỊ sau khi lọc. Đây là chủ ý thiết kế, giúp thao tác nhanh trên từng nhóm.', WARN),
  new Paragraph({ children: [new PageBreak()] })
);

// ---------- 9. Chọn ----------
children.push(
  H1('9. Chọn phần mềm để cài'),
  P('Tích trực tiếp vào ô ở cột "Cài", hoặc dùng các nút chọn nhanh:'),
  table(
    [2600, 6400],
    ['Nút', 'Tác dụng'],
    [
      ['Chọn tất cả', 'Tích chọn mọi dòng đang hiển thị'],
      ['Bỏ chọn', 'Bỏ tích mọi dòng đang hiển thị'],
      ['Đảo chọn', 'Đảo ngược trạng thái tích của mọi dòng đang hiển thị'],
      ['Chỉ cái chưa cài', 'Chỉ tích những phần mềm máy chưa có — dùng nhiều nhất trong thực tế']
    ],
    { zebra: true }
  ),
  spacer(140),
  P('Dòng chữ tổng kết phía dưới bảng luôn cho biết: tổng số phần mềm, số đang chọn và số đã cài. Hãy đọc dòng này trước khi bấm cài đặt.'),
  new Paragraph({ children: [new PageBreak()] })
);

// ---------- 10. Cài đặt ----------
children.push(
  H1('10. Chạy cài đặt hàng loạt'),

  H2('10.1. Hộp xác nhận'),
  P('Sau khi bấm "Bắt đầu cài đặt", công cụ hiện hộp xác nhận liệt kê đầy đủ các phần mềm sắp cài kèm trạng thái hiện tại của từng cái. Hãy đọc kỹ danh sách này.'),
  P('Trong hộp xác nhận có hai lựa chọn quan trọng cho phần mềm ĐÃ CÓ trên máy:'),
  table(
    [2600, 6400],
    ['Lựa chọn', 'Khi nào dùng'],
    [
      ['Bỏ qua (khuyến nghị)', 'Cài máy mới, hoặc không muốn động vào phiên bản người dùng đang dùng ổn định. Chạy nhanh hơn nhiều vì không tải lại.'],
      ['Nâng cấp', 'Đợt rà soát cập nhật phần mềm định kỳ, hoặc khi cần vá lỗi bảo mật. Chạy lâu hơn vì phải tải bản mới.']
    ],
    { zebra: true }
  ),
  spacer(140),
  P('Lựa chọn này được ghi nhớ cho các lần sau.'),

  H2('10.2. Theo dõi tiến trình'),
  P('Trong lúc chạy, khu vực dưới bảng hiển thị: tên phần mềm đang xử lý, thanh tiến trình và số đếm dạng "3/10". Cột Trạng thái của từng dòng cũng cập nhật ngay khi cài xong.'),
  callout('Bình thường:',
    'Có phần mềm cài trong 10 giây, có phần mềm mất vài phút. Bộ Office hoặc IDE lập trình có thể mất trên 10 phút. Cửa sổ không bị treo — nếu thấy tiến trình vẫn nhúc nhích thì mọi thứ đang chạy đúng.', OK),

  H2('10.3. Huỷ giữa chừng'),
  step('Bấm nút "Huỷ cài đặt" (nút này chỉ hiện khi đang chạy).', 'steps-cancel'),
  step('Chờ vài giây để phần mềm đang cài dở dừng lại.', 'steps-cancel'),
  step('Các phần mềm chưa tới lượt sẽ được ghi nhận là "Đã huỷ" và có thể chạy lại sau.', 'steps-cancel'),
  callout('Khi bấm nút X lúc đang cài:',
    'Ứng dụng sẽ hỏi lại "Đóng ứng dụng bây giờ sẽ huỷ các gói còn lại. Bạn có chắc muốn thoát?". Chọn No để tiếp tục cài bình thường. Dù đã có lớp hỏi này, cách làm đúng vẫn là bấm "Huỷ cài đặt" trước, chờ gói hiện tại dừng hẳn rồi mới đóng cửa sổ.', WARN),
  new Paragraph({ children: [new PageBreak()] })
);

// ---------- 11. Kết quả và lỗi ----------
children.push(
  H1('11. Đọc kết quả và xử lý lỗi'),
  P('Chạy xong, một bảng tổng kết hiện ra: số thành công, bỏ qua, thất bại, bị huỷ và tổng thời gian. Chi tiết từng phần mềm nằm ở thẻ "Kết quả cài đặt".'),

  H2('11.1. Ý nghĩa các kết quả'),
  table(
    [2400, 6600],
    ['Kết quả', 'Ý nghĩa'],
    [
      [{ t: 'Thành công', color: OK, bold: true }, 'Đã cài mới xong'],
      [{ t: 'Đã nâng cấp', color: OK, bold: true }, 'Đã cập nhật lên bản mới hơn'],
      [{ t: 'Bỏ qua', color: '767676', bold: true }, 'Máy đã có sẵn, người dùng chọn Bỏ qua'],
      [{ t: 'Đã có sẵn', color: '767676', bold: true }, 'Đã là bản mới nhất, không có gì để cập nhật'],
      [{ t: 'Đã huỷ', color: WARN, bold: true }, 'Bị dừng do người dùng bấm Huỷ'],
      [{ t: 'Thất bại', color: BAD, bold: true }, 'Cài lỗi — xem cột Exit code và Thông báo']
    ],
    { zebra: true }
  ),

  H2('11.2. Bảng tra mã lỗi thường gặp'),
  P('Mã lỗi (Exit code) hiển thị ở thẻ "Kết quả cài đặt" và thẻ "Nhật ký".'),
  table(
    [2000, 3200, 3800],
    ['Exit code', 'Nguyên nhân', 'Cách xử lý'],
    [
      [{ t: '0x8A150014', mono: true }, 'Không tìm thấy gói trong kho WinGet', 'Kiểm tra lại chính tả Package Id; dùng thẻ "Tìm trên WinGet" để lấy Id đúng'],
      [{ t: '0x8A150019', mono: true }, 'Lệnh cần quyền Administrator', 'Bấm "Chạy bằng quyền Admin" rồi thử lại phần lỗi'],
      [{ t: '0x80070005', mono: true }, 'Bị từ chối quyền truy cập', 'Như trên; nếu vẫn lỗi, kiểm tra chính sách bảo mật của máy'],
      [{ t: '0x8A150056', mono: true }, 'Trình cài đặt không cho chạy dưới quyền Admin', 'Đóng công cụ, mở lại bằng quyền người dùng thường'],
      [{ t: '0x8A150008', mono: true }, 'Tải file cài đặt thất bại', 'Kiểm tra mạng, thử lại; nếu công ty chặn, báo quản trị mạng'],
      [{ t: '0x8A150102', mono: true }, 'Đang có tiến trình cài đặt khác', 'Chờ Windows Update hoặc trình cài khác chạy xong rồi thử lại'],
      [{ t: '0x8A150101', mono: true }, 'Phần mềm đang chạy / file đang bị khoá', 'Đóng phần mềm đó trên máy rồi thử lại'],
      [{ t: '0x8A150105', mono: true }, 'Ổ đĩa đã đầy', 'Dọn dung lượng ổ C rồi thử lại'],
      [{ t: '0x8A150109', mono: true }, 'Cài xong nhưng cần khởi động lại', 'Không phải lỗi — khởi động lại máy sau khi cài hết'],
      [{ t: '0x8A150010', mono: true }, 'Không có bản cài phù hợp với máy', 'Gói không hỗ trợ phiên bản Windows này — tìm gói thay thế']
    ],
    { zebra: true }
  ),

  H2('11.3. Thử lại phần bị lỗi'),
  P('Không cần tích chọn lại thủ công. Chỉ cần bấm nút "Thử lại phần lỗi" — công cụ tự chạy lại đúng những phần mềm có kết quả Thất bại.'),
  callout('Quan trọng cần nhớ:',
    'Một phần mềm lỗi KHÔNG làm dừng cả hàng đợi. Các phần mềm phía sau vẫn được cài bình thường, nên cứ để chạy hết rồi mới xử lý phần lỗi.', OK),
  new Paragraph({ children: [new PageBreak()] })
);

// ---------- 12. Nhật ký ----------
children.push(
  H1('12. Nhật ký và báo lỗi lên cấp trên'),
  P('Thẻ "Nhật ký" ghi lại toàn bộ: thời điểm, mức độ, nội dung, câu lệnh đã chạy và mã lỗi.'),
  table(
    [2600, 6400],
    ['Nút', 'Công dụng'],
    [
      ['Xoá nhật ký', 'Xoá các dòng đang hiển thị trên màn hình (file log trên đĩa vẫn còn)'],
      ['Sao chép tất cả', 'Chép toàn bộ nhật ký vào clipboard để dán vào email hoặc ticket'],
      ['Mở thư mục log', 'Mở thư mục Logs chứa file log theo ngày'],
      ['Tự cuộn xuống dòng mới', 'Tự động cuộn theo dòng mới nhất khi đang cài']
    ],
    { zebra: true }
  ),

  H2('12.1. Cần gửi gì khi báo lỗi'),
  step('Tên phần mềm và WinGet Package Id bị lỗi.', 'steps-report'),
  step('Mã Exit code trong thẻ "Kết quả cài đặt".', 'steps-report'),
  step('Ảnh chụp màn hình thẻ "Kết quả cài đặt".', 'steps-report'),
  step('File log trong thư mục Logs (bấm "Mở thư mục log" để lấy).', 'steps-report'),
  step('Phiên bản Windows của máy đích và cho biết máy có vào mạng được không.', 'steps-report'),
  new Paragraph({ children: [new PageBreak()] })
);

// ---------- 13. Chuẩn hoá ----------
children.push(
  H1('13. Chuẩn hoá danh sách cho cả đội'),
  P('Để mọi kỹ thuật viên dùng chung một bộ chuẩn, nên phân công một người phụ trách danh sách rồi phát hành cho cả đội.'),

  H2('13.1. Người phụ trách xuất danh sách'),
  step('Dựng và kiểm tra danh sách trên máy của mình.', 'steps-export'),
  step('Bấm "Xuất tất cả" ở thanh dưới cùng để xuất toàn bộ cấu hình ra một file JSON.', 'steps-export'),
  step('Đặt tên file có kèm ngày, ví dụ: danh-sach-phan-mem-2026-09-07.json', 'steps-export'),
  step('Đưa file lên thư mục dùng chung của bộ phận IT.', 'steps-export'),
  P('Nếu chỉ cần chia sẻ một bộ duy nhất, dùng nút "Xuất cấu hình này" để xuất riêng cấu hình đang chọn.'),

  H2('13.2. Kỹ thuật viên nhập danh sách'),
  step('Bấm "Nhập JSON" ở thanh dưới cùng, chọn file được phát hành.', 'steps-import'),
  stepR([
    'Hộp thoại hỏi cách nhập. Chọn ',
    { text: 'Yes', bold: true },
    ' để THAY THẾ toàn bộ danh sách hiện tại bằng bản chuẩn; chọn ',
    { text: 'No', bold: true },
    ' để THÊM các cấu hình mới vào bên cạnh danh sách đang có.'
  ], 'steps-import'),
  step('Kiểm tra ô chọn cấu hình ở góc trên bên phải đã có các cấu hình mới.', 'steps-import'),
  callout('Khuyến nghị:',
    'Trước khi bấm Yes (thay thế), nên "Xuất tất cả" một bản để dự phòng.', WARN),

  H2('13.3. Sửa trực tiếp file JSON (dành cho người có kinh nghiệm)'),
  P('File Data/software-list.json có thể mở bằng Notepad để sửa hàng loạt. Cấu trúc một mục phần mềm:'),
  code(
    '{\n' +
    '  "name": "Google Chrome",\n' +
    '  "packageId": "Google.Chrome",\n' +
    '  "category": "Browser",\n' +
    '  "source": "winget",\n' +
    '  "isSelected": true,\n' +
    '  "sortOrder": 0\n' +
    '}'
  ),
  P('Giá trị hợp lệ của category: Browser, Development, Office, Entertainment, Utility, Other.'),
  callout('Cơ chế bảo vệ:',
    'Nếu file JSON bị hỏng, công cụ tự đổi tên nó thành đuôi .bak, tạo lại danh sách mẫu và ghi cảnh báo vào nhật ký — dữ liệu cũ không bị mất, vẫn nằm trong file .bak.', OK),
  new Paragraph({ children: [new PageBreak()] })
);

// ---------- 14. Bảng Package Id ----------
children.push(
  H1('14. Bảng Package Id thông dụng'),
  P('Toàn bộ Package Id dưới đây đã được kiểm chứng bằng lệnh winget search trên kho WinGet chính thức tại thời điểm biên soạn tài liệu. Kho WinGet thay đổi theo thời gian, nên khi thêm mới vẫn nên đối chiếu lại bằng thẻ "Tìm trên WinGet".'),

  H2('14.1. Trình duyệt'),
  table(
    [3000, 3600, 2400],
    ['Phần mềm', 'WinGet Package Id', 'Ghi chú'],
    [
      ['Google Chrome', { t: 'Google.Chrome', mono: true }, 'Phổ biến nhất'],
      ['Mozilla Firefox', { t: 'Mozilla.Firefox', mono: true }, 'Bản tiếng Anh'],
      ['Cốc Cốc', { t: 'CocCoc.CocCoc', mono: true }, 'Trình duyệt Việt Nam']
    ],
    { zebra: true }
  ),

  H2('14.2. Văn phòng và tài liệu'),
  table(
    [3000, 3600, 2400],
    ['Phần mềm', 'WinGet Package Id', 'Ghi chú'],
    [
      ['Microsoft 365 Apps', { t: 'Microsoft.Office', mono: true }, 'Cần tài khoản bản quyền'],
      ['LibreOffice', { t: 'TheDocumentFoundation.LibreOffice', mono: true }, 'Miễn phí'],
      ['Adobe Acrobat Reader', { t: 'Adobe.Acrobat.Reader.64-bit', mono: true }, 'Chọn bản 64-bit'],
      ['Foxit PDF Reader', { t: 'Foxit.FoxitReader', mono: true }, 'Nhẹ hơn Acrobat'],
      ['UniKey', { t: 'UniKey.UniKey', mono: true }, 'Gõ tiếng Việt']
    ],
    { zebra: true }
  ),

  H2('14.3. Liên lạc và họp trực tuyến'),
  table(
    [3000, 3600, 2400],
    ['Phần mềm', 'WinGet Package Id', 'Ghi chú'],
    [
      ['Zalo', { t: 'VNGCorp.Zalo', mono: true }, 'Bản máy tính'],
      ['Microsoft Teams', { t: 'Microsoft.Teams', mono: true }, 'Bản mới'],
      ['Zoom Workplace', { t: 'Zoom.Zoom', mono: true }, ''],
      ['Telegram Desktop', { t: 'Telegram.TelegramDesktop', mono: true }, '']
    ],
    { zebra: true }
  ),

  H2('14.4. Hỗ trợ từ xa'),
  table(
    [3000, 3600, 2400],
    ['Phần mềm', 'WinGet Package Id', 'Ghi chú'],
    [
      ['UltraViewer', { t: 'DucFabulous.UltraViewer', mono: true }, 'Phổ biến tại Việt Nam'],
      ['AnyDesk', { t: 'AnyDesk.AnyDesk', mono: true }, ''],
      ['TeamViewer', { t: 'TeamViewer.TeamViewer', mono: true }, 'Chú ý bản quyền khi dùng thương mại']
    ],
    { zebra: true }
  ),

  H2('14.5. Tiện ích hệ thống'),
  table(
    [3000, 3600, 2400],
    ['Phần mềm', 'WinGet Package Id', 'Ghi chú'],
    [
      ['7-Zip', { t: '7zip.7zip', mono: true }, 'Nén/giải nén, miễn phí'],
      ['WinRAR', { t: 'RARLab.WinRAR', mono: true }, 'Cần bản quyền'],
      ['Notepad++', { t: 'Notepad++.Notepad++', mono: true }, 'Soạn thảo văn bản thuần'],
      ['Everything', { t: 'voidtools.Everything', mono: true }, 'Tìm file cực nhanh'],
      ['Microsoft PowerToys', { t: 'Microsoft.PowerToys', mono: true }, 'Bộ tiện ích của Microsoft'],
      ['Windows Terminal', { t: 'Microsoft.WindowsTerminal', mono: true }, ''],
      ['Google Drive', { t: 'Google.GoogleDrive', mono: true }, 'Đồng bộ tài liệu'],
      ['Dropbox', { t: 'Dropbox.Dropbox', mono: true }, '']
    ],
    { zebra: true }
  ),

  H2('14.6. Đa phương tiện'),
  table(
    [3000, 3600, 2400],
    ['Phần mềm', 'WinGet Package Id', 'Ghi chú'],
    [
      ['VLC media player', { t: 'VideoLAN.VLC', mono: true }, 'Xem được hầu hết định dạng'],
      ['K-Lite Codec Pack', { t: 'CodecGuide.K-LiteCodecPack.Standard', mono: true }, 'Bộ codec']
    ],
    { zebra: true }
  ),

  H2('14.7. Công cụ lập trình'),
  table(
    [3000, 3600, 2400],
    ['Phần mềm', 'WinGet Package Id', 'Ghi chú'],
    [
      ['Visual Studio Code', { t: 'Microsoft.VisualStudioCode', mono: true }, ''],
      ['Git', { t: 'Git.Git', mono: true }, ''],
      ['Node.js', { t: 'OpenJS.NodeJS', mono: true }, 'Bản mới nhất'],
      ['Python 3.13', { t: 'Python.Python.3.13', mono: true }, 'Chọn phiên bản theo dự án'],
      ['Eclipse Temurin JDK 17', { t: 'EclipseAdoptium.Temurin.17.JDK', mono: true }, 'Java bản mã nguồn mở']
    ],
    { zebra: true }
  ),
  new Paragraph({ children: [new PageBreak()] })
);

// ---------- 15. FAQ ----------
children.push(
  H1('15. Câu hỏi thường gặp'),
  table(
    [3400, 5600],
    ['Câu hỏi', 'Trả lời'],
    [
      ['Máy đích có cần cài .NET trước không?', 'Không. Bản phát hành đã nhúng sẵn .NET runtime bên trong file .exe.'],
      ['Có cần cài công cụ này lên máy đích không?', 'Không. Chỉ cần chép thư mục vào máy rồi chạy file .exe.'],
      ['Chạy trực tiếp từ USB được không?', 'Được. Nhưng nên chép vào ổ cứng để chạy nhanh hơn và tránh rút nhầm USB giữa chừng.'],
      ['Có cần quyền Administrator không?', 'Mặc định là không. Chỉ nâng quyền khi gặp mã lỗi liên quan tới quyền.'],
      ['Không có mạng thì dùng được không?', 'Không cài được. WinGet phải tải gói từ Internet. Danh sách vẫn xem và sửa được bình thường.'],
      ['Công cụ có tự tải file .exe từ web lạ không?', 'Không. Toàn bộ việc tải do WinGet thực hiện từ kho chính thức của Microsoft.'],
      ['Có lưu mật khẩu hay tài khoản không?', 'Không. File dữ liệu chỉ chứa tên phần mềm và Package Id.'],
      ['Cài được phần mềm nội bộ của công ty không?', 'Chỉ khi phần mềm đó có trên kho WinGet. Phần mềm nội bộ không đưa lên WinGet thì vẫn phải cài thủ công.'],
      ['Đóng công cụ có mất danh sách không?', 'Không. Mọi thay đổi được lưu ngay xuống file, không cần bấm nút Lưu.'],
      ['Cài lại một phần mềm đã có được không?', 'Được — chọn "Nâng cấp" trong hộp xác nhận. Nếu đã là bản mới nhất, kết quả sẽ là "Đã có sẵn".'],
      ['Vì sao có phần mềm hiện "Chưa cài" dù máy rõ ràng đã có?', 'Vì phần mềm đó được cài bằng cách khác (không qua WinGet) nên Windows ghi nhận mã định danh khác. Chọn "Bỏ qua" hoặc bỏ tick dòng đó.'],
      ['Đang cài dở mà đóng cửa sổ thì sao?', 'Không nên. Hãy bấm "Huỷ cài đặt", chờ dừng hẳn rồi mới đóng.'],
      ['Cài được nhiều máy cùng lúc không?', 'Được — mỗi máy chạy một bản chép riêng. Trên cùng MỘT máy thì các gói luôn cài lần lượt để tránh xung đột trình cài đặt.']
    ],
    { zebra: true }
  ),
  new Paragraph({ children: [new PageBreak()] })
);

// ---------- 16. Checklist ----------
children.push(
  H1('16. Phiếu thao tác nhanh'),
  P('Có thể in riêng trang này và dán cạnh bàn làm việc.'),
  spacer(120),
  table(
    [800, 5400, 2800],
    ['TT', 'Thao tác', 'Xác nhận'],
    [
      ['1', 'Chép thư mục WindowsSetupAssistant vào máy đích', '☐'],
      ['2', 'Mở WindowsSetupAssistant.exe (SmartScreen: More info → Run anyway)', '☐'],
      ['3', 'Không có khung cảnh báo cam → WinGet đã sẵn sàng', '☐'],
      ['4', 'Chọn đúng cấu hình cho loại máy đang cài', '☐'],
      ['5', 'Bấm "Kiểm tra đã cài", chờ cột Trạng thái cập nhật xong', '☐'],
      ['6', 'Bấm "Chỉ cái chưa cài"', '☐'],
      ['7', 'Rà lại danh sách đã tích, bỏ tick phần mềm không cần', '☐'],
      ['8', 'Bấm "Bắt đầu cài đặt", đọc hộp xác nhận, chọn Bỏ qua / Nâng cấp', '☐'],
      ['9', 'Chờ chạy xong, đọc bảng tổng kết', '☐'],
      ['10', 'Có lỗi → bấm "Thử lại phần lỗi"', '☐'],
      ['11', 'Vẫn lỗi → tra bảng mã lỗi ở mục 11.2', '☐'],
      ['12', 'Mở thẻ "Kết quả cài đặt", chụp màn hình lưu hồ sơ bàn giao', '☐'],
      ['13', 'Khởi động lại máy nếu có phần mềm yêu cầu', '☐'],
      ['14', 'Rút USB đúng cách (Safely Remove)', '☐']
    ],
    { zebra: true }
  ),

  H2('16.1. Những điều tuyệt đối không làm'),
  bulletR([{ text: 'Không ', bold: true, color: BAD }, 'chép mỗi file .exe mà bỏ lại thư mục Data.']),
  bulletR([{ text: 'Không ', bold: true, color: BAD }, 'tắt cửa sổ bằng nút X khi đang cài dở — hãy bấm "Huỷ cài đặt" trước.']),
  bulletR([{ text: 'Không ', bold: true, color: BAD }, 'rút USB khi công cụ đang chạy từ USB.']),
  bulletR([{ text: 'Không ', bold: true, color: BAD }, 'chạy công cụ bằng quyền Administrator nếu không thật sự cần.']),
  bulletR([{ text: 'Không ', bold: true, color: BAD }, 'gõ Package Id theo trí nhớ — luôn đối chiếu bằng thẻ "Tìm trên WinGet".']),
  bulletR([{ text: 'Không ', bold: true, color: BAD }, 'sửa file JSON trong lúc công cụ đang mở.'])
);

// ================================================================ TẠO FILE
const doc = new Document({
  creator: 'Bộ phận IT',
  title: 'Hướng dẫn sử dụng Windows Setup Assistant',
  description: 'Tài liệu hướng dẫn sử dụng dành cho kỹ thuật viên IT và IT Helpdesk',
  styles: {
    default: {
      document: { run: { font: 'Segoe UI', size: 21, color: '212121' } }
    },
    paragraphStyles: [
      { id: 'Heading1', name: 'Heading 1', basedOn: 'Normal', next: 'Normal', quickFormat: true,
        run: { font: 'Segoe UI', size: 30, bold: true, color: ACCENT } },
      { id: 'Heading2', name: 'Heading 2', basedOn: 'Normal', next: 'Normal', quickFormat: true,
        run: { font: 'Segoe UI', size: 25, bold: true, color: ACCENT } }
    ]
  },
  numbering: {
    config: [
      {
        reference: 'bullets',
        levels: [
          { level: 0, format: LevelFormat.BULLET, text: '\u2022', alignment: AlignmentType.LEFT,
            style: { paragraph: { indent: { left: 460, hanging: 260 } } } },
          { level: 1, format: LevelFormat.BULLET, text: '\u25E6', alignment: AlignmentType.LEFT,
            style: { paragraph: { indent: { left: 900, hanging: 260 } } } }
        ]
      },
      ...['steps-open', 'steps-smart', 'steps-admin', 'steps-winget', 'steps-flow',
          'steps-addA', 'steps-addB', 'steps-edit', 'steps-del', 'steps-cancel',
          'steps-report', 'steps-export', 'steps-import'].map(ref => ({
        reference: ref,
        levels: [{
          level: 0, format: LevelFormat.DECIMAL, text: '%1.', alignment: AlignmentType.LEFT,
          style: { paragraph: { indent: { left: 460, hanging: 260 } } }
        }]
      }))
    ]
  },
  sections: [{
    properties: {
      page: {
        margin: {
          top: convertInchesToTwip(0.9),
          bottom: convertInchesToTwip(0.9),
          left: convertInchesToTwip(1),
          right: convertInchesToTwip(1)
        }
      }
    },
    children
  }]
});

Packer.toBuffer(doc).then(buffer => {
  fs.writeFileSync(process.argv[2], buffer);
  console.log('Da tao: ' + process.argv[2]);
});
