/**
 * WebDesk ERP - Reusable Dynamic DataTables Component Engine
 * Handles initialization, DOM layout, per-column filters, and global defaults.
 */
(function (window, $) {
    'use strict';

    if (!$) {
        console.error('ERPDataTables requires jQuery.');
        return;
    }

    // Suppress intrusive browser alert popups for DataTables warnings; log to console instead
    if ($.fn && $.fn.dataTable && $.fn.dataTable.ext) {
        $.fn.dataTable.ext.errMode = 'console';
    }

    var ERPDataTables = {
        /**
         * Default configuration options for all ERP tables
         */
        defaults: {
            dom: '<"dt-header-controls d-flex flex-wrap justify-content-between align-items-center gap-3 mb-3"lf>t<"dt-footer-controls d-flex flex-wrap justify-content-between align-items-center gap-3 mt-3 pt-3 border-top"ip>',
            pageLength: 10,
            lengthMenu: [10, 25, 50, 100],
            orderCellsTop: true,
            order: [], // Preserve database descending order; prevents DataTables from flipping column 0 to ascending
            autoWidth: false,
            responsive: false,
            language: {
                search: '',
                searchPlaceholder: 'Search across all columns...',
                lengthMenu: 'Show _MENU_ entries',
                info: 'Showing _START_ to _END_ of _TOTAL_ records',
                infoFiltered: '(filtered from _MAX_ total)',
                infoEmpty: 'Showing 0 to 0 of 0 records',
                zeroRecords: '<i class="fa fa-folder-open fa-2x mb-2 d-block text-muted opacity-50"></i>No matching records found.',
                emptyTable: '<i class="fa fa-folder-open fa-2x mb-2 d-block text-muted opacity-50"></i>No records available.',
                paginate: {
                    first: '«',
                    previous: '‹',
                    next: '›',
                    last: '»'
                }
            }
        },

        /**
         * Initialize a DataTable with auto-wired column filters
         * @param {string|jQuery} selector - CSS selector or jQuery object for the table
         * @param {object} customOptions - Optional custom DataTable options to override defaults
         * @returns {DataTable} The initialized DataTable API instance
         */
        init: function (selector, customOptions) {
            var $table = $(selector);
            if (!$table.length) return null;

            // If already initialized, return existing instance
            if ($.fn.DataTable.isDataTable($table)) {
                return $table.DataTable();
            }

            // Detect non-sortable columns from th attributes or class
            var columnDefs = [];
            $table.find('thead tr:first th').each(function (idx) {
                var isNoSort = $(this).hasClass('no-sort') || $(this).data('dt-no-sort') === true || $(this).data('dt-no-sort') === 'true';
                if (isNoSort) {
                    columnDefs.push({ orderable: false, targets: idx });
                }
            });

            // Merge options: defaults -> auto columnDefs -> customOptions
            var mergedOptions = $.extend(true, {}, this.defaults, {
                columnDefs: columnDefs
            }, customOptions);

            // Initialize DataTable
            var tableApi = $table.DataTable(mergedOptions);

            // Auto-wire Row-2 per-column filter inputs and selects
            this._wireColumnFilters($table, tableApi);

            return tableApi;
        },

        /**
         * Auto-wires text inputs and select dropdowns found in thead .dt-col-filter
         */
        _wireColumnFilters: function ($table, tableApi) {
            var $filterRow = $table.find('thead tr.dt-col-filter');
            if (!$filterRow.length) return;

            $filterRow.find('th').each(function (colIdx) {
                var $th = $(this);

                // Text search input
                var $input = $th.find('input');
                if ($input.length) {
                    $input.on('keyup change clear', function () {
                        if (tableApi.column(colIdx).search() !== this.value) {
                            tableApi.column(colIdx).search(this.value).draw();
                        }
                    });
                }

                // Select dropdown filter
                var $select = $th.find('select');
                if ($select.length) {
                    $select.on('change', function () {
                        var val = this.value;
                        // Exact match for select dropdown
                        tableApi.column(colIdx).search(val ? '^' + $.fn.dataTable.util.escapeRegex(val) + '$' : '', true, false).draw();
                    });
                }
            });
        }
    };

    // Global Confirmation Handler for delete buttons across all tables
    $(document).on('click', '.btn-delete-confirm', function (e) {
        e.preventDefault();
        var message = $(this).data('confirm-message') || 'Are you sure you want to delete this item?';
        var $form = $(this).closest('form');

        if (confirm(message)) {
            if ($form.length) {
                $form.submit();
            } else if ($(this).attr('href')) {
                window.location.href = $(this).attr('href');
            }
        }
    });

    // Auto-init tables on DOM ready if marked with .dynamic-table
    $(document).ready(function () {
        $('.dynamic-table').each(function () {
            ERPDataTables.init(this);
        });
    });

    // Export to global scope
    window.ERPDataTables = ERPDataTables;

})(window, window.jQuery);
