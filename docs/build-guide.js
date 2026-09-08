const fs = require('fs');
const path = require('path');
const {
  Document, Packer, Paragraph, TextRun, HeadingLevel, AlignmentType,
  Table, TableRow, TableCell, WidthType, ShadingType, BorderStyle,
  PageBreak, LevelFormat, convertInchesToTwip, ImageRun
} = require('docx');

// ---------------------------------------------------------------- Hằng số màu sắc & bố cục
const ACCENT = '1F4E79';              // Xanh Navy
const ACCENT_LIGHT = 'EBF1F5';        // Xanh nhạt tiêu đề bảng
const GREY_LIGHT = 'F4F4F4';          // Xám nhạt code block
const OK = '2E7D32';                  // Xanh lá
const WARN = 'B26A00';                // Cam cảnh báo
const BAD = 'C00000';                 // Đỏ lỗi / cấm

// ---------------------------------------------------------------- Helper định dạng
const P = (text, opts = {}) => new Paragraph({
  spacing: { after: opts.after ?? 70, line: 240 },
  alignment: opts.align,
  children: [new TextRun({
    text,
    bold: opts.bold,
    italics: opts.italics,
    size: opts.size ?? 20,
    color: opts.color ?? '212121',
    font: opts.font ?? 'Segoe UI'
  })]
});

const PR = (runs, opts = {}) => new Paragraph({
  spacing: { after: opts.after ?? 70, line: 240 },
  indent: opts.indent,
  alignment: opts.align,
  children: runs.map(r => typeof r === 'string'
    ? new TextRun({ text: r, font: 'Segoe UI', size: 20, color: '212121' })
    : new TextRun({ font: 'Segoe UI', size: 20, color: '212121', ...r }))
});

const H1 = (text) => new Paragraph({
  heading: HeadingLevel.HEADING_1,
  spacing: { before: 240, after: 100 },
  keepNext: true,
  children: [new TextRun({ text, bold: true, size: 26, color: ACCENT, font: 'Segoe UI' })]
});

const H2 = (text) => new Paragraph({
  heading: HeadingLevel.HEADING_2,
  spacing: { before: 180, after: 80 },
  keepNext: true,
  children: [new TextRun({ text, bold: true, size: 22, color: ACCENT, font: 'Segoe UI' })]
});

const bulletR = (runs) => new Paragraph({
  numbering: { reference: 'bullets', level: 0 },
  spacing: { after: 50, line: 230 },
  children: runs.map(r => typeof r === 'string'
    ? new TextRun({ text: r, font: 'Segoe UI', size: 20, color: '212121' })
    : new TextRun({ font: 'Segoe UI', size: 20, color: '212121', ...r }))
});

const code = (text) => new Paragraph({
  spacing: { before: 50, after: 70 },
  keepLines: true,
  shading: { type: ShadingType.CLEAR, fill: GREY_LIGHT },
  border: {
    top: { style: BorderStyle.SINGLE, size: 4, color: 'D0D0D0' },
    bottom: { style: BorderStyle.SINGLE, size: 4, color: 'D0D0D0' },
    left: { style: BorderStyle.SINGLE, size: 4, color: 'D0D0D0' },
    right: { style: BorderStyle.SINGLE, size: 4, color: 'D0D0D0' }
  },
  indent: { left: 100, right: 100 },
  children: [new TextRun({ text, font: 'Consolas', size: 17 })]
});

const callout = (label, text, color) => new Paragraph({
  spacing: { before: 90, after: 100 },
  keepLines: true,
  shading: { type: ShadingType.CLEAR, fill: 'FAFAFA' },
  border: { left: { style: BorderStyle.SINGLE, size: 16, color } },
  indent: { left: 140, right: 100 },
  children: [
    new TextRun({ text: label + ' ', bold: true, color, font: 'Segoe UI', size: 19 }),
    new TextRun({ text, font: 'Segoe UI', size: 19, color: '333333' })
  ]
});

const cell = (text, opts = {}) => new TableCell({
  width: { size: opts.width, type: WidthType.DXA },
  shading: opts.fill ? { type: ShadingType.CLEAR, fill: opts.fill } : undefined,
  margins: { top: 60, bottom: 60, left: 100, right: 100 },
  children: (Array.isArray(text) ? text : [text]).map(t => new Paragraph({
    spacing: { after: 0, line: 230 },
    alignment: opts.align,
    children: [new TextRun({
      text: t,
      bold: opts.bold,
      color: opts.color ?? (opts.bold ? ACCENT : '212121'),
      font: opts.mono ? 'Consolas' : 'Segoe UI',
      size: opts.mono ? 17 : 19
    })]
  }))
});

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
          align: o.align,
          fill: opts.zebra && ri % 2 === 1 ? 'FBFBFB' : undefined
        });
      })
    }))
  ]
});

const figure = (imageRelPath, caption, width = 560, height = 350) => {
  const fullPath = path.resolve(__dirname, imageRelPath);
  if (!fs.existsSync(fullPath)) {
    console.warn('Image not found: ' + fullPath);
    return [P('[Ảnh minh hoạ: ' + caption + ']', { italics: true, color: '888888' })];
  }
  const imgBuffer = fs.readFileSync(fullPath);
  return [
    new Paragraph({
      alignment: AlignmentType.CENTER,
      spacing: { before: 120, after: 50 },
      keepWithNext: true,
      children: [
        new ImageRun({
          data: imgBuffer,
          transformation: { width, height }
        })
      ]
    }),
    new Paragraph({
      alignment: AlignmentType.CENTER,
      spacing: { after: 120 },
      children: [
        new TextRun({
          text: caption,
          italics: true,
          size: 18,
          color: '595959',
          font: 'Segoe UI',
          bold: true
        })
      ]
    })
  ];
};

const spacer = (h = 100) => new Paragraph({ spacing: { after: h }, children: [] });

// ================================================================ NỘI DUNG TÀI LIỆU
const children = [];

// ---------- TIÊU ĐỀ ĐẦU TRANG ----------
children.push(
  new Paragraph({
    alignment: AlignmentType.CENTER,
    spacing: { before: 80, after: 50 },
    children: [new TextRun({ text: 'HƯỚNG DẪN SỬ DỤNG CHI TIẾT & TRỰC QUAN', bold: true, size: 28, color: ACCENT, font: 'Segoe UI' })]
  }),
  new Paragraph({
    alignment: AlignmentType.CENTER,
    spacing: { after: 100 },
    children: [new TextRun({ text: 'WINDOWS SETUP ASSISTANT (v1.0.1)', bold: true, size: 38, color: ACCENT, font: 'Segoe UI' })]
  }),
  new Paragraph({
    alignment: AlignmentType.CENTER,
    spacing: { after: 180 },
    border: { bottom: { style: BorderStyle.SINGLE, size: 6, color: ACCENT } },
    children: [new TextRun({
      text: 'Công cụ Cài đặt hàng loạt & Quét sao lưu phần mềm máy tính dành cho Kỹ thuật viên IT & Người dùng',
      size: 20, color: '595959', font: 'Segoe UI', italics: true
    })]
  }),
  callout('Tóm tắt nhanh:', 'Không cần cài đặt (.NET đã nhúng sẵn). Chạy trên Windows 10 (1809+) và Windows 11 (64-bit). Cài đặt an toàn 100% qua kho WinGet chính thức của Microsoft, loại bỏ hoàn toàn nguy cơ mã độc hay link rác.', OK)
);

// ---------- 1. Chuẩn bị & Khởi động ----------
children.push(
  H1('1. Chuẩn bị & Khởi động ứng dụng'),
  P('Ứng dụng ở dạng Portable (không cần cài đặt). Hãy sao chép nguyên thư mục vào máy tính hoặc USB kỹ thuật viên:'),
  code(
    'Thư mục bộ cài (USB hoặc ổ đĩa máy tính)/\n' +
    '└── WindowsSetupAssistant/\n' +
    '    ├── WindowsSetupAssistant.exe   ← File chạy chính (~63 MB, nhúng sẵn runtime .NET)\n' +
    '    ├── Data/software-list.json     ← DANH SÁCH CẤU HÌNH (Bắt buộc phải có bên cạnh)\n' +
    '    └── Logs/                       ← Thư mục nhật ký lưu lại lịch sử các lần cài đặt'
  ),
  callout('Cảnh báo sống còn:', 'Phải chép NGUYÊN CẢ THƯ MỤC WindowsSetupAssistant. Nếu chỉ chép lẻ loi file .exe thì toàn bộ danh mục cấu hình và phần mềm sẽ không hiển thị!', BAD),
  spacer(60),

  H2('1.1. Tổng quan giao diện ứng dụng'),
  P('Khi mở WindowsSetupAssistant.exe, màn hình chính hiển thị rõ ràng các phân vùng chức năng:'),
  ...figure('images/01_tong_quan_giao_dien.png', 'Hình 1: Tổng quan giao diện chính của Windows Setup Assistant', 560, 355),
  spacer(60),
  table(
    [2200, 6800],
    ['Khu vực', 'Chức năng & Ý nghĩa thực tế'],
    [
      [
        'Thanh tiêu đề (Trên)',
        'Hiển thị tiêu đề app, ô chọn Cấu hình (Profile), các nút quản lý Cấu hình (Mới, Đổi tên, Nhân bản, Xoá) và nút chuyển đổi Giao diện Sáng/Tối.'
      ],
      [
        '4 Tab làm việc',
        '• Danh sách phần mềm: Nơi xem, lọc và chọn các phần mềm cần cài đặt.\n' +
        '• Tìm trên WinGet: Tra cứu trực tiếp từ kho ứng dụng Microsoft để lấy Package Id chuẩn.\n' +
        '• Kết quả cài đặt: Bảng tóm tắt kết quả thành công/thất bại của từng phần mềm.\n' +
        '• Nhật ký: Ghi lại từng câu lệnh winget, thời gian và mã exit code để kiểm tra.'
      ],
      [
        'Thanh công cụ lọc',
        'Ô tìm kiếm phần mềm nhanh, danh sách chọn Nhóm (Trình duyệt, Văn phòng, Tiện ích...), các nút chọn thông minh (Chọn tất cả, Bỏ chọn, Đảo chọn, Chỉ cái chưa cài), và các nút Thêm / Sửa / Xoá gói.'
      ],
      [
        'Thanh hành động (Dưới)',
        'Tóm tắt số lượng phần mềm đã chọn, nút "Quét & sao lưu máy này", "Kiểm tra đã cài", "Thử lại phần lỗi", và nút "Bắt đầu cài đặt".'
      ],
      [
        'Thanh trạng thái đáy',
        'Hiển thị đường dẫn file dữ liệu đang dùng, mức quyền hiện tại (Người dùng thường / Admin), nút "Chạy bằng quyền Admin", "Mở thư mục dữ liệu", "Nhập JSON" và "Xuất cấu hình".'
      ]
    ],
    { zebra: true }
  ),
  spacer(80),

  H2('1.2. Xử lý 3 tình huống khi mở máy'),
  table(
    [2700, 3100, 3200],
    ['Tình huống', 'Hiện tượng', 'Cách xử lý trong 5 giây'],
    [
      [
        'SmartScreen cảnh báo',
        'Màn hình xanh "Windows protected your PC"',
        'Bấm "More info" (Thông tin thêm) → Bấm "Run anyway" (Vẫn chạy).'
      ],
      [
        'Khung cảnh báo cam',
        'Báo máy chưa có WinGet',
        'Bấm nút "Mở Microsoft Store" trên khung → Cài "App Installer" → Mở lại app.'
      ],
      [
        'Cần quyền Administrator?',
        'Có gói cài báo lỗi quyền truy cập',
        'Mặc định chạy quyền thường. Chỉ bấm "Chạy bằng quyền Admin" ở góc dưới khi cần.'
      ]
    ],
    { zebra: true }
  ),
  new Paragraph({ children: [new PageBreak()] })
);

// ---------- 2. Chức năng 1: Cài đặt hàng loạt ----------
children.push(
  H1('2. Chức năng 1: Cài đặt hàng loạt cho máy mới (1 phút thao tác)'),
  P('Dùng khi bàn giao máy mới hoặc máy vừa cài lại Windows. Chỉ cần thực hiện đúng 4 bước sau:'),
  table(
    [800, 2400, 5800],
    ['Bước', 'Thao tác', 'Hướng dẫn chi tiết'],
    [
      [
        '1',
        'Chọn Cấu hình',
        'Tại ô chọn ở góc trên bên phải, chọn Profile phù hợp với vị trí sử dụng (ví dụ: "Máy văn phòng", "Máy lập trình", "Máy kế toán", "Máy cá nhân"...).'
      ],
      [
        '2',
        'Kiểm tra đã cài',
        'Bấm nút "Kiểm tra đã cài" ở thanh dưới cùng. Chờ 3–5 giây, ứng dụng sẽ quét và hiển thị trạng thái thực tế của máy.'
      ],
      [
        '3',
        'Tích chọn thông minh',
        'Bấm nút "Chỉ cái chưa cài" để app tự động tick chọn đúng những phần mềm còn thiếu. Bạn có thể bỏ tick những app máy này không cần.'
      ],
      [
        '4',
        'Bắt đầu cài đặt',
        'Bấm nút "Bắt đầu cài đặt" (màu xanh dương). Hộp thoại xác nhận sẽ hiện lên để bạn chọn chế độ cài đặt an toàn.'
      ]
    ],
    { zebra: true }
  ),
  spacer(80),

  H2('2.1. Trạng thái sau khi bấm "Kiểm tra đã cài"'),
  P('Ứng dụng tự động phân biệt rõ ràng phần mềm nào máy đã có và phần mềm nào còn thiếu:'),
  ...figure('images/02_kiem_tra_da_cai.png', 'Hình 2: Trạng thái phần mềm sau khi bấm "Kiểm tra đã cài" (xanh lá = đã có, xám = chưa cài)', 560, 355),
  spacer(60),
  bulletR([{ text: 'Màu xanh lá (Đã có sẵn): ', bold: true, color: OK }, 'Máy tính đã cài phần mềm này kèm số hiệu phiên bản hiện tại. App tự động bỏ tick để tránh cài đè lãng phí thời gian.']),
  bulletR([{ text: 'Màu xám (Chưa cài): ', bold: true, color: '555555' }, 'Máy tính chưa có phần mềm này. Khi bấm "Chỉ cái chưa cài", các dòng này sẽ được tự động tick chọn sẵn sàng.']),
  spacer(80),

  H2('2.2. Hộp thoại Xác nhận cài đặt'),
  P('Trước khi tiến hành cài đặt, ứng dụng hiển thị hộp thoại xác nhận để người dùng kiểm tra lại danh sách:'),
  ...figure('images/03_hop_thoai_xac_nhan.png', 'Hình 3: Hộp thoại xác nhận danh sách cài đặt và lựa chọn phương thức xử lý', 380, 344),
  spacer(60),
  bulletR([
    { text: 'Tùy chọn 1 - Bỏ qua (Khuyên dùng): ', bold: true, color: ACCENT },
    'Không can thiệp vào các phần mềm máy đã có sẵn, chỉ cài đặt những phần mềm còn thiếu. Đây là chế độ nhanh và an toàn nhất.'
  ]),
  bulletR([
    { text: 'Tùy chọn 2 - Nâng cấp (winget upgrade): ', bold: true, color: WARN },
    'Nếu máy đã có phiên bản cũ, WinGet sẽ tự động tải bản cập nhật mới nhất từ trang chủ nhà phát triển.'
  ]),
  callout('Cơ chế hàng đợi an toàn:', 'Các phần mềm được cài đặt LẦN LƯỢT (tuần tự), không cài song song để tránh xung đột file hệ thống. Nếu có 1 phần mềm bị lỗi, ứng dụng vẫn tiếp tục cài các phần mềm tiếp theo mà không bị ngắt quãng giữa chừng!', OK),
  new Paragraph({ children: [new PageBreak()] })
);

// ---------- 3. Chức năng 2: Quét & sao lưu máy này ----------
children.push(
  H1('3. Chức năng 2: "Quét & sao lưu máy này" (Chuyển máy cũ sang máy mới)'),
  PR([
    'Chức năng ',
    { text: '"Quét & sao lưu máy này"', bold: true, color: ACCENT },
    ' là trợ thủ đắc lực giúp kỹ thuật viên lưu lại toàn bộ ứng dụng người dùng đang làm việc trên máy tính cũ trước khi cài lại Windows hoặc trước khi chuyển sang laptop mới.'
  ]),
  spacer(60),
  ...figure('images/06_quet_sao_luu_may.png', 'Hình 4: Cửa sổ kết quả quét và phân loại phần mềm trên máy tính hiện tại', 520, 390),
  spacer(60),
  table(
    [2500, 6500],
    ['Giai đoạn', 'Hướng dẫn từng bước thực hiện chi tiết'],
    [
      [
        'Giai đoạn 1:\nTrên máy cũ\n(Quét & Xuất file)',
        '1. Mở WindowsSetupAssistant.exe trên máy cũ của người dùng.\n' +
        '2. Bấm nút "Quét & sao lưu máy này" ở thanh công cụ dưới cùng.\n' +
        '3. Chờ 15–30 giây để WinGet rà soát toàn bộ phần mềm đã cài đặt trên hệ thống.\n' +
        '4. Cửa sổ "Kết quả quét" xuất hiện (Hình 4): Kiểm tra danh sách và bỏ tick các app rác hoặc app cá nhân không cần thiết.\n' +
        '5. Bấm nút "Lưu bản sao lưu" → Đặt tên file theo mã máy hoặc tên nhân viên (ví dụ: PC-KeToan-01.json). Ứng dụng tự động xuất ra 2 file:\n' +
        '   • File .json: Chứa profile các gói WinGet để cài tự động trên máy mới.\n' +
        '   • File -cai-tay.csv: Chứa danh sách các phần mềm nội bộ chuyên biệt cần cài thủ công.'
      ],
      [
        'Giai đoạn 2:\nTrên máy mới\n(Khôi phục lại)',
        '1. Cắm USB chứa 2 file sao lưu vào máy tính mới.\n' +
        '2. Mở WindowsSetupAssistant.exe, bấm nút "Nhập JSON" ở góc dưới bên phải và chọn file .json đã lưu.\n' +
        '3. Cấu hình máy cũ sẽ xuất hiện ngay trong danh sách chọn Profile.\n' +
        '4. Bấm "Kiểm tra đã cài" → Bấm "Chỉ cái chưa cài" → Bấm "Bắt đầu cài đặt".\n' +
        '5. Sau khi cài xong hàng đợi WinGet, mở file -cai-tay.csv bằng Excel để đối chiếu và cài nốt các phần mềm đặc thù còn lại (ví dụ phần mềm kế toán MISA, chữ ký số, thuế).'
      ]
    ],
    { zebra: true }
  ),
  new Paragraph({ children: [new PageBreak()] })
);

// ---------- 4. Thêm phần mềm & Quản lý cấu hình ----------
children.push(
  H1('4. Thêm phần mềm mới & Quản lý Cấu hình (Profile)'),
  
  H2('4.1. Thêm phần mềm an toàn qua WinGet (Khuyến nghị số 1)'),
  P('Kho ứng dụng WinGet của Microsoft chứa hàng chục ngàn phần mềm phổ biến. Hãy dùng tab này để lấy Package Id chuẩn 100%:'),
  ...figure('images/04_tab_tim_kiem_winget.png', 'Hình 5: Tìm kiếm phần mềm trực tiếp từ kho WinGet chính thức của Microsoft', 560, 355),
  spacer(60),
  bulletR([{ text: 'Bước 1: ', bold: true }, 'Chuyển sang tab ', { text: 'Tìm trên WinGet', bold: true }, ' ở thanh tab phía trên.']),
  bulletR([{ text: 'Bước 2: ', bold: true }, 'Gõ tên phần mềm cần tìm (tiếng Anh không dấu, ví dụ: ', { text: 'zalo, foxit, notion, telegram, unikey, chrome', font: 'Consolas' }, ') → Bấm ', { text: 'Tìm kiếm', bold: true }, '.']),
  bulletR([{ text: 'Bước 3: ', bold: true }, 'Tại dòng kết quả mong muốn, bấm nút ', { text: 'Thêm vào danh sách', bold: true }, '.']),
  bulletR([{ text: 'Bước 4: ', bold: true }, 'Chọn Nhóm phần mềm (Trình duyệt, Văn phòng, Tiện ích, Lập trình...) rồi bấm ', { text: 'Lưu', bold: true }, '.']),
  callout('Ưu điểm tuyệt đối:', 'Dùng chức năng tìm kiếm giúp bạn lấy đúng mã Package Id chuẩn xác do chính nhà phát triển đăng ký với Microsoft, không bao giờ lo gõ nhầm tên hay tải nhầm phần mềm giả mạo.', OK),
  spacer(80),

  H2('4.2. Hộp thoại Thêm / Sửa phần mềm thủ công'),
  P('Bạn có thể chỉnh sửa tên hiển thị, nhóm danh mục hoặc ghi chú cho từng phần mềm bất cứ lúc nào:'),
  ...figure('images/05_them_sua_phan_mem.png', 'Hình 6: Hộp thoại Sửa thông tin phần mềm (Name, Package Id, Category, Notes)', 370, 350),
  spacer(60),
  bulletR([{ text: 'Tên phần mềm: ', bold: true }, 'Tên thân thiện hiển thị trên danh sách (ví dụ: Foxit PDF Reader).']),
  bulletR([{ text: 'WinGet Package Id: ', bold: true }, 'Mã định danh duy nhất của gói cài trên WinGet (ví dụ: ', { text: 'Foxit.FoxitReader', font: 'Consolas' }, ').']),
  bulletR([{ text: 'Nhóm phần mềm: ', bold: true }, 'Phân loại theo chức năng (Văn phòng, Trình duyệt, Đồ hoạ, Tiện ích, Giải trí, Lập trình...).']),
  bulletR([{ text: 'Ghi chú: ', bold: true }, 'Mô tả thêm tính năng hoặc hướng dẫn đăng nhập cho người dùng.']),
  spacer(80),

  H2('4.3. Quản lý Cấu hình & Chia sẻ danh sách trong nội bộ IT'),
  bulletR([
    { text: 'Tạo cấu hình mới siêu nhanh: ', bold: true },
    'Chọn một cấu hình mẫu có sẵn → Bấm nút ',
    { text: 'Nhân bản', bold: true },
    ' → Đặt tên mới (ví dụ: nhân bản từ "Máy văn phòng" thành "Máy Kế toán") rồi thêm/bớt phần mềm tùy ý mà không làm ảnh hưởng cấu hình gốc.'
  ]),
  bulletR([
    { text: 'Chia sẻ cấu hình cho đồng nghiệp: ', bold: true },
    'Người phụ trách bấm ',
    { text: 'Xuất tất cả', bold: true },
    ' ở thanh dưới cùng để gửi file .json cho cả đội. Kỹ thuật viên khác chỉ cần bấm ',
    { text: 'Nhập JSON', bold: true },
    ' là toàn bộ danh sách profile chuẩn của công ty sẽ được cập nhật đồng bộ 100%.'
  ]),
  new Paragraph({ children: [new PageBreak()] })
);

// ---------- 5. Bảng cứu nguy: Xử lý sự cố ----------
children.push(
  H1('5. Bảng cứu nguy: Xử lý sự cố nhanh & Nguyên tắc vàng'),
  
  H2('5.1. Tab Nhật ký hệ thống (Tra cứu khi gặp sự cố)'),
  P('Mọi câu lệnh winget, thời gian thực thi và mã trả về (Exit code) đều được ghi lại đầy đủ theo thời gian thực:'),
  ...figure('images/07_tab_nhat_ky.png', 'Hình 7: Tab Nhật ký hệ thống ghi nhận chi tiết từng câu lệnh WinGet đã thực thi', 560, 355),
  spacer(60),
  bulletR([{ text: 'Xoá nhật ký: ', bold: true }, 'Làm sạch danh sách log trên màn hình để chuẩn bị cho lượt cài mới.']),
  bulletR([{ text: 'Sao chép tất cả: ', bold: true }, 'Copy toàn bộ nội dung log vào Clipboard để gửi cho chuyên viên hỗ trợ cấp trên.']),
  bulletR([{ text: 'Mở thư mục log: ', bold: true }, 'Mở thư mục chứa các file nhật ký theo ngày trên ổ đĩa để lưu trữ hoặc gửi báo cáo.']),
  spacer(80),

  H2('5.2. Nhận biết màu sắc tại cột Trạng thái'),
  table(
    [2200, 6800],
    ['Màu sắc', 'Ý nghĩa thực tế'],
    [
      [{ t: 'Xanh lá', color: OK, bold: true }, 'Đã có sẵn trên máy, hoặc vừa cài đặt / nâng cấp thành công.'],
      [{ t: 'Xám', color: '767676', bold: true }, 'Chưa cài đặt, hoặc đã được bỏ qua vì máy đã có sẵn bản này.'],
      [{ t: 'Cam', color: WARN, bold: true }, 'Bị dừng do người dùng chủ động bấm "Huỷ cài đặt".'],
      [{ t: 'Đỏ', color: BAD, bold: true }, 'Cài đặt thất bại (bấm nút "Thử lại phần lỗi" ở thanh dưới để chạy lại).']
    ],
    { zebra: true }
  ),
  spacer(80),

  H2('5.3. Bốn mã lỗi thường gặp nhất & Cách giải quyết trong 1 bước'),
  table(
    [2000, 3200, 3800],
    ['Mã lỗi', 'Nguyên nhân', 'Cách xử lý trong 1 bước'],
    [
      [
        { t: '0x8A150019\n0x80070005', mono: true },
        'Yêu cầu quyền Administrator',
        'Bấm "Chạy bằng quyền Admin" ở góc dưới bên phải rồi bấm "Thử lại phần lỗi".'
      ],
      [
        { t: '0x8A150102', mono: true },
        'Đang kẹt tiến trình khác',
        'Windows Update đang chạy ngầm. Đợi 2-3 phút rồi bấm "Thử lại phần lỗi".'
      ],
      [
        { t: '0x8A150101', mono: true },
        'Phần mềm đang mở',
        'Tắt hẳn phần mềm đó trên máy tính rồi bấm "Thử lại phần lỗi".'
      ],
      [
        { t: '0x8A150008', mono: true },
        'Lỗi mạng / tải file',
        'Kiểm tra lại kết nối mạng Internet hoặc đường truyền proxy/firewall.'
      ]
    ],
    { zebra: true }
  ),
  spacer(80),

  H2('5.4. Bốn điều TUYỆT ĐỐI TRÁNH dành cho Kỹ thuật viên'),
  bulletR([{ text: '1. Không ', bold: true, color: BAD }, 'chép lẻ loi file .exe ra USB mà bỏ quên thư mục Data/ chứa danh mục phần mềm.']),
  bulletR([{ text: '2. Không ', bold: true, color: BAD }, 'tắt đột ngột bằng nút [X] khi đang cài — hãy bấm nút "Huỷ cài đặt", đợi dừng hẳn rồi mới thoát.']),
  bulletR([{ text: '3. Không ', bold: true, color: BAD }, 'rút USB khi phần mềm đang chạy trực tiếp trên USB.']),
  bulletR([{ text: '4. Không ', bold: true, color: BAD }, 'chạy quyền Admin ngay từ đầu nếu không gặp mã lỗi yêu cầu quyền.'])
);

// ---------- 6. Cài hàng loạt bằng một dòng lệnh ----------
children.push(
  H1('6. Cài hàng loạt bằng một dòng lệnh'),
  P('Khi phải cài cho nhiều máy trong cùng một buổi, bạn không cần mở ứng dụng trên từng máy. ' +
    'Chuẩn bị cấu hình một lần trong giao diện, sau đó chạy một dòng lệnh trên mỗi máy.'),
  callout('QUAN TRỌNG',
    'Mở PowerShell bằng quyền Administrator TRƯỚC khi gõ lệnh. Chế độ này cố ý không hiện hộp ' +
    'thoại UAC, nên nếu thiếu quyền thì một số phần mềm sẽ cài trượt mà không có gì báo cho bạn.',
    WARN),
  code('.\\WindowsSetupAssistant.exe --unattended --profile "Máy công ty"'),
  H2('Các tham số'),
  table([2600, 1800, 4600],
    ['Tham số', 'Mặc định', 'Ý nghĩa'],
    [
      ['--unattended', '(bắt buộc)', 'Bật chế độ. Không có nó thì ứng dụng mở giao diện như cũ.'],
      ['--profile <tên>', 'cấu hình đang chọn', 'Cấu hình cần cài'],
      ['--existing skip|upgrade', 'skip', 'Gói đã có trên máy: bỏ qua hay nâng cấp'],
      ['--report <đường dẫn>', 'thư mục Reports', 'Nơi ghi file báo cáo'],
      ['--help', '', 'In hướng dẫn']
    ]),
  H2('Đọc kết quả'),
  P('Sau khi chạy xong, gõ echo $LASTEXITCODE trong PowerShell để biết kết quả:'),
  table([1400, 7600],
    ['Mã thoát', 'Ý nghĩa'],
    [
      ['0', 'Mọi phần mềm đã xử lý xong'],
      ['1', 'Có ít nhất một phần mềm cài trượt — mở file báo cáo để xem gói nào'],
      ['2', 'Gõ sai tham số, hoặc không có cấu hình tên đó'],
      ['3', 'Máy chưa có WinGet — cài App Installer trước'],
      ['4', 'Bạn đã nhấn Ctrl+C để dừng']
    ]),
  P('File báo cáo JSON nằm trong thư mục Reports cạnh file .exe. Hãy lưu file này lại làm hồ sơ ' +
    'bàn giao máy: nó ghi rõ máy nào, lúc nào, cài những gì, gói nào trượt và vì sao.')
);

// ================================================================ TẠO FILE DOCX
const doc = new Document({
  creator: 'Bộ phận IT',
  title: 'Hướng dẫn sử dụng chi tiết Windows Setup Assistant',
  description: 'Tài liệu hướng dẫn sử dụng trực quan kèm ảnh chụp thực tế cho kỹ thuật viên IT và người dùng mới',
  styles: {
    default: {
      document: { run: { font: 'Segoe UI', size: 20, color: '212121' } }
    },
    paragraphStyles: [
      { id: 'Heading1', name: 'Heading 1', basedOn: 'Normal', next: 'Normal', quickFormat: true,
        run: { font: 'Segoe UI', size: 26, bold: true, color: ACCENT } },
      { id: 'Heading2', name: 'Heading 2', basedOn: 'Normal', next: 'Normal', quickFormat: true,
        run: { font: 'Segoe UI', size: 22, bold: true, color: ACCENT } }
    ]
  },
  numbering: {
    config: [
      {
        reference: 'bullets',
        levels: [
          { level: 0, format: LevelFormat.BULLET, text: '\u2022', alignment: AlignmentType.LEFT,
            style: { paragraph: { indent: { left: 400, hanging: 220 } } } }
        ]
      }
    ]
  },
  sections: [{
    properties: {
      page: {
        margin: {
          top: convertInchesToTwip(0.8),
          bottom: convertInchesToTwip(0.8),
          left: convertInchesToTwip(0.8),
          right: convertInchesToTwip(0.8)
        }
      }
    },
    children
  }]
});

const outputPath = process.argv[2] || path.resolve(__dirname, 'Huong-dan-su-dung-Windows-Setup-Assistant.docx');
Packer.toBuffer(doc).then(buffer => {
  fs.writeFileSync(outputPath, buffer);
  console.log('Da tao thanh cong tai lieu Word: ' + outputPath + ' (Kich thuoc: ' + Math.round(buffer.length / 1024) + ' KB)');
});
