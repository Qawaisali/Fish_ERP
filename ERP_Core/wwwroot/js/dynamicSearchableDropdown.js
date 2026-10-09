/**
 * DynamicSearchableDropdown.js
 * 
 * Reusable component to turn any standard HTML <select> into a searchable dropdown.
 * - Powered by TomSelect (lightweight, zero-jQuery dependency).
 * - Auto-loads TomSelect CSS & JS from CDN if not already loaded on the page.
 * - Works with ES6 modules or standard <script> tags.
 * - Supports both local static options and remote server-side search (AJAX).
 */

(function (global) {
    const TOM_SELECT_CDN_CSS = 'https://cdn.jsdelivr.net/npm/tom-select@2.3.1/dist/css/tom-select.bootstrap5.min.css';
    const TOM_SELECT_CDN_JS = 'https://cdn.jsdelivr.net/npm/tom-select@2.3.1/dist/js/tom-select.complete.min.js';

    /**
     * Loads a script dynamically if not present
     */
    function loadScript(url) {
        return new Promise((resolve, reject) => {
            if (window.TomSelect) return resolve();
            const existingScript = document.querySelector(`script[src="${url}"]`);
            if (existingScript) {
                existingScript.addEventListener('load', () => resolve());
                existingScript.addEventListener('error', (e) => reject(e));
                return;
            }
            const script = document.createElement('script');
            script.src = url;
            script.onload = () => resolve();
            script.onerror = (e) => reject(new Error(`Failed to load script: ${url}`));
            document.head.appendChild(script);
        });
    }

    /**
     * Loads a stylesheet dynamically if not present
     */
    function loadStylesheet(url) {
        if (!document.querySelector(`link[href="${url}"]`)) {
            const link = document.createElement('link');
            link.rel = 'stylesheet';
            link.href = url;
            const firstLink = document.head.querySelector('link[rel="stylesheet"], style');
            if (firstLink) {
                document.head.insertBefore(link, firstLink);
            } else {
                document.head.appendChild(link);
            }
        }
    }

    /**
     * Searchable Select Class
     */
    class SearchableDropdown {
        /**
         * @param {string|HTMLElement} target - CSS selector or select DOM element
         * @param {Object} options - Configuration options
         */
        constructor(target, options = {}) {
            this.target = target;
            this.options = Object.assign({
                placeholder: 'Type to search...',
                allowEmptyOption: true,
                create: false,               // Allow adding custom entries if true
                maxItems: 1,                 // 1 for single-select, null for multi-select
                remoteUrl: null,             // Endpoint URL for server-side search
                valueField: 'id',            // Remote JSON field name for value
                labelField: 'text',          // Remote JSON field name for display label
                searchField: ['text', 'value'],       // Fields to search on
                pageSize: 20,
                onChange: null
            }, options);

            this.instances = [];
            this.init();
        }

        async init() {
            try {
                // Ensure CDN dependencies are loaded
                loadStylesheet(TOM_SELECT_CDN_CSS);
                await loadScript(TOM_SELECT_CDN_JS);

                const elements = typeof this.target === 'string'
                    ? Array.from(document.querySelectorAll(this.target))
                    : [this.target];

                elements.forEach(selectEl => {
                    if (!selectEl || selectEl.tagName !== 'SELECT') return;

                    // If already initialized with TomSelect, destroy it first
                    if (selectEl.tomselect) {
                        selectEl.tomselect.destroy();
                    }

                    const config = this._buildConfig(selectEl);
                    const instance = new TomSelect(selectEl, config);

                    if (instance.wrapper) {
                        instance.wrapper.classList.add('form-select-dark');
                    }

                    instance.on('change', (val) => {
                        if (typeof this.options.onChange === 'function') {
                            this.options.onChange(val, instance);
                        }
                        try {
                            selectEl.dispatchEvent(new Event('change', { bubbles: true }));
                            selectEl.dispatchEvent(new Event('input', { bubbles: true }));
                        } catch (e) {
                            if (typeof selectEl.onchange === 'function') {
                                selectEl.onchange();
                            }
                        }
                    });

                    this.instances.push(instance);
                });
            } catch (err) {
                console.error('[DynamicSearchableDropdown] Initialization error:', err);
            }
        }

        _buildConfig(selectEl) {
            const placeholder = selectEl.getAttribute('data-placeholder') ||
                selectEl.getAttribute('placeholder') ||
                this.options.placeholder;

            const plugins = this.options.plugins || (selectEl.hasAttribute('multiple') ? ['remove_button'] : ['dropdown_input']);

            const config = {
                placeholder: placeholder,
                allowEmptyOption: this.options.allowEmptyOption,
                create: this.options.create,
                maxItems: selectEl.hasAttribute('multiple') ? null : this.options.maxItems,
                maxOptions: null, // Allow all options to be rendered without TomSelect 50-item cutoff
                searchField: this.options.searchField || ['text', 'value'],
                sortField: { field: '$order' },
                plugins: plugins
            };

            if (this.options.tomSelectOptions) {
                Object.assign(config, this.options.tomSelectOptions);
            }

            // Remote search configuration (server-side AJAX)
            if (this.options.remoteUrl) {
                config.valueField = this.options.valueField;
                config.labelField = this.options.labelField;
                config.searchField = this.options.searchField;
                config.loadThrottle = 300;
                config.shouldLoad = (query) => query.length >= 0;
                config.load = async (query, callback) => {
                    try {
                        const separator = this.options.remoteUrl.includes('?') ? '&' : '?';
                        const url = `${this.options.remoteUrl}${separator}query=${encodeURIComponent(query)}&pageSize=${this.options.pageSize}`;
                        const response = await fetch(url, {
                            headers: { 'Accept': 'application/json' }
                        });
                        if (!response.ok) throw new Error(`HTTP error ${response.status}`);
                        const data = await response.json();
                        // Accepts either array directly or { items: [...] } / { data: [...] }
                        const items = Array.isArray(data) ? data : (data.items || data.data || []);
                        callback(items);
                    } catch (e) {
                        console.error('[DynamicSearchableDropdown] Remote search failed:', e);
                        callback();
                    }
                };
            }

            return config;
        }

        /**
         * Get initialized TomSelect instances
         */
        getInstances() {
            return this.instances;
        }

        /**
         * Destroy instances
         */
        destroy() {
            this.instances.forEach(inst => inst.destroy());
            this.instances = [];
        }
    }

    /**
     * Convenience helper function
     * Example: initSearchableSelect('#mySelect', { placeholder: 'Select an item' });
     */
    function initSearchableSelect(selector, options) {
        return new SearchableDropdown(selector, options);
    }

    // Auto-init selects with [data-searchable="true"] or class .searchable-select
    if (typeof document !== 'undefined') {
        const autoInit = () => {
            document.querySelectorAll('select[data-searchable="true"], select.searchable-select').forEach(el => {
                if (!el.tomselect) {
                    new SearchableDropdown(el);
                }
            });
        };
        if (document.readyState === 'loading') {
            document.addEventListener('DOMContentLoaded', autoInit);
        } else {
            autoInit();
        }
    }

    // Expose globally
    global.SearchableDropdown = SearchableDropdown;
    global.initSearchableSelect = initSearchableSelect;

    // Support ES Module export if available
    if (typeof module !== 'undefined' && module.exports) {
        module.exports = { SearchableDropdown, initSearchableSelect };
    }
})(typeof window !== 'undefined' ? window : this);

