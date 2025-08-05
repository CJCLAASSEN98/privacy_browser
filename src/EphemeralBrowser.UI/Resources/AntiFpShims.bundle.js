(function() {
    'use strict';

    // Configuration and feature flags
    const SHIM_CONFIG = {
        canvas: { enabled: true, noiseLevel: 0.01 },
        audio: { enabled: true, noiseLevel: 0.001 },
        webgl: { enabled: true, vendor: 'EphemeralBrowser', renderer: 'Generic GPU' },
        timing: { enabled: true, quantum: 100 },
        battery: { enabled: true },
        screen: { enabled: true },
        timezone: { enabled: true },
        fonts: { enabled: true },
        languages: { enabled: true }
    };

    // Per-site override mechanism
    const SITE_OVERRIDES = {
        'example.com': { canvas: { enabled: false } },
        'banking.com': { timing: { enabled: false } }
    };

    // Breakage safelist - sites where specific shims cause issues
    const BREAKAGE_SAFELIST = {
        'maps.google.com': ['webgl', 'canvas'],
        'youtube.com': ['audio'],
        'netflix.com': ['timing', 'screen']
    };

    // Apply site-specific configuration
    function applyShimConfig() {
        const hostname = window.location.hostname;
        const override = SITE_OVERRIDES[hostname];
        const breakageList = BREAKAGE_SAFELIST[hostname] || [];
        
        if (override) {
            Object.assign(SHIM_CONFIG, override);
        }
        
        // Disable shims for sites with known breakage
        breakageList.forEach(shimName => {
            if (SHIM_CONFIG[shimName]) {
                SHIM_CONFIG[shimName].enabled = false;
            }
        });
    }

    // Canvas fingerprint protection
    function shimCanvas() {
        if (!SHIM_CONFIG.canvas.enabled) return;

        const CanvasPrototype = CanvasRenderingContext2D.prototype;
        const originalGetImageData = CanvasPrototype.getImageData;
        const originalToDataURL = HTMLCanvasElement.prototype.toDataURL;
        const originalToBlob = HTMLCanvasElement.prototype.toBlob;

        function addCanvasNoise(imageData) {
            if (!imageData || !imageData.data) return imageData;
            
            const data = imageData.data;
            const noiseLevel = SHIM_CONFIG.canvas.noiseLevel;
            
            // Add subtle noise to prevent fingerprinting
            for (let i = 0; i < data.length; i += 4) {
                if (Math.random() < 0.1) { // Only modify 10% of pixels
                    const noise = (Math.random() - 0.5) * noiseLevel * 255;
                    data[i] = Math.max(0, Math.min(255, data[i] + noise));     // R
                    data[i + 1] = Math.max(0, Math.min(255, data[i + 1] + noise)); // G
                    data[i + 2] = Math.max(0, Math.min(255, data[i + 2] + noise)); // B
                }
            }
            
            return imageData;
        }

        CanvasPrototype.getImageData = function(...args) {
            const imageData = originalGetImageData.apply(this, args);
            return addCanvasNoise(imageData);
        };

        HTMLCanvasElement.prototype.toDataURL = function(...args) {
            const context = this.getContext('2d');
            if (context) {
                const imageData = context.getImageData(0, 0, this.width, this.height);
                addCanvasNoise(imageData);
                context.putImageData(imageData, 0, 0);
            }
            return originalToDataURL.apply(this, args);
        };

        HTMLCanvasElement.prototype.toBlob = function(callback, ...args) {
            const context = this.getContext('2d');
            if (context) {
                const imageData = context.getImageData(0, 0, this.width, this.height);
                addCanvasNoise(imageData);
                context.putImageData(imageData, 0, 0);
            }
            return originalToBlob.call(this, callback, ...args);
        };
    }

    // Audio fingerprint protection
    function shimAudio() {
        if (!SHIM_CONFIG.audio.enabled) return;

        try {
            const AudioContext = window.AudioContext || window.webkitAudioContext;
            if (!AudioContext) return;

            const OriginalAnalyser = AudioContext.prototype.createAnalyser;
            const OriginalOscillator = AudioContext.prototype.createOscillator;

            AudioContext.prototype.createAnalyser = function() {
                const analyser = OriginalAnalyser.call(this);
                const originalGetByteFrequencyData = analyser.getByteFrequencyData;
                const originalGetFloatFrequencyData = analyser.getFloatFrequencyData;

                analyser.getByteFrequencyData = function(array) {
                    originalGetByteFrequencyData.call(this, array);
                    // Add subtle noise to frequency data
                    for (let i = 0; i < array.length; i++) {
                        if (Math.random() < 0.1) {
                            const noise = (Math.random() - 0.5) * SHIM_CONFIG.audio.noiseLevel * 255;
                            array[i] = Math.max(0, Math.min(255, array[i] + noise));
                        }
                    }
                };

                analyser.getFloatFrequencyData = function(array) {
                    originalGetFloatFrequencyData.call(this, array);
                    // Add subtle noise to frequency data
                    for (let i = 0; i < array.length; i++) {
                        if (Math.random() < 0.1) {
                            const noise = (Math.random() - 0.5) * SHIM_CONFIG.audio.noiseLevel * 100;
                            array[i] = Math.max(-100, Math.min(0, array[i] + noise));
                        }
                    }
                };

                return analyser;
            };
        } catch (e) {
            console.warn('EphemeralBrowser: Audio shim failed:', e.message);
        }
    }

    // WebGL fingerprint protection
    function shimWebGL() {
        if (!SHIM_CONFIG.webgl.enabled) return;

        const getParameter = WebGLRenderingContext.prototype.getParameter;
        WebGLRenderingContext.prototype.getParameter = function(parameter) {
            switch (parameter) {
                case this.VENDOR:
                    return SHIM_CONFIG.webgl.vendor;
                case this.RENDERER:
                    return SHIM_CONFIG.webgl.renderer;
                case this.VERSION:
                    return 'WebGL 1.0 (EphemeralBrowser)';
                case this.SHADING_LANGUAGE_VERSION:
                    return 'WebGL GLSL ES 1.0 (EphemeralBrowser)';
                default:
                    return getParameter.call(this, parameter);
            }
        };

        // Also handle WebGL2
        if (window.WebGL2RenderingContext) {
            const getParameter2 = WebGL2RenderingContext.prototype.getParameter;
            WebGL2RenderingContext.prototype.getParameter = function(parameter) {
                switch (parameter) {
                    case this.VENDOR:
                        return SHIM_CONFIG.webgl.vendor;
                    case this.RENDERER:
                        return SHIM_CONFIG.webgl.renderer;
                    case this.VERSION:
                        return 'WebGL 2.0 (EphemeralBrowser)';
                    case this.SHADING_LANGUAGE_VERSION:
                        return 'WebGL GLSL ES 3.0 (EphemeralBrowser)';
                    default:
                        return getParameter2.call(this, parameter);
                }
            };
        }
    }

    // High-resolution timing protection
    function shimTiming() {
        if (!SHIM_CONFIG.timing.enabled) return;

        const quantum = SHIM_CONFIG.timing.quantum;
        const originalNow = Performance.prototype.now;

        Performance.prototype.now = function() {
            const time = originalNow.call(this);
            return Math.floor(time / quantum) * quantum;
        };

        // Also shim Date.now for consistency
        const originalDateNow = Date.now;
        Date.now = function() {
            const time = originalDateNow();
            return Math.floor(time / quantum) * quantum;
        };
    }

    // Battery API blocking
    function shimBattery() {
        if (!SHIM_CONFIG.battery.enabled) return;

        if ('getBattery' in navigator) {
            navigator.getBattery = function() {
                return Promise.reject(new Error('Battery API disabled by EphemeralBrowser'));
            };
        }

        // Block battery property access
        Object.defineProperty(navigator, 'battery', {
            get: function() {
                return undefined;
            },
            configurable: false
        });
    }

    // Screen fingerprint protection
    function shimScreen() {
        if (!SHIM_CONFIG.screen.enabled) return;

        // Common screen resolutions to blend in
        const commonResolutions = [
            { width: 1920, height: 1080 },
            { width: 1366, height: 768 },
            { width: 1536, height: 864 },
            { width: 1440, height: 900 }
        ];

        const resolution = commonResolutions[Math.floor(Math.random() * commonResolutions.length)];

        Object.defineProperties(screen, {
            width: { value: resolution.width, writable: false },
            height: { value: resolution.height, writable: false },
            availWidth: { value: resolution.width, writable: false },
            availHeight: { value: resolution.height - 40, writable: false }, // Account for taskbar
            colorDepth: { value: 24, writable: false },
            pixelDepth: { value: 24, writable: false }
        });
    }

    // Timezone fingerprint protection
    function shimTimezone() {
        if (!SHIM_CONFIG.timezone.enabled) return;

        const originalGetTimezoneOffset = Date.prototype.getTimezoneOffset;
        Date.prototype.getTimezoneOffset = function() {
            // Return UTC offset (0) to prevent timezone fingerprinting
            return 0;
        };

        // Override Intl.DateTimeFormat timezone detection
        if (window.Intl && window.Intl.DateTimeFormat) {
            const originalResolvedOptions = Intl.DateTimeFormat.prototype.resolvedOptions;
            Intl.DateTimeFormat.prototype.resolvedOptions = function() {
                const options = originalResolvedOptions.call(this);
                options.timeZone = 'UTC';
                return options;
            };
        }
    }

    // Font enumeration protection
    function shimFonts() {
        if (!SHIM_CONFIG.fonts.enabled) return;

        // Common system fonts to report
        const commonFonts = [
            'Arial', 'Helvetica', 'Times New Roman', 'Courier New',
            'Verdana', 'Georgia', 'Palatino', 'Garamond',
            'Bookman', 'Comic Sans MS', 'Trebuchet MS', 'Arial Black'
        ];

        // Override font detection methods
        if (document.fonts && document.fonts.check) {
            const originalCheck = document.fonts.check;
            document.fonts.check = function(font, text) {
                // Only report common fonts as available
                const fontFamily = font.split(' ').pop().replace(/['"]/g, '');
                if (commonFonts.includes(fontFamily)) {
                    return originalCheck.call(this, font, text);
                }
                return false;
            };
        }
    }

    // Language fingerprint protection
    function shimLanguages() {
        if (!SHIM_CONFIG.languages.enabled) return;

        // Report only English to prevent language-based fingerprinting
        Object.defineProperty(navigator, 'language', {
            value: 'en-US',
            writable: false
        });

        Object.defineProperty(navigator, 'languages', {
            value: ['en-US', 'en'],
            writable: false
        });
    }

    // Plugin enumeration blocking
    function shimPlugins() {
        Object.defineProperty(navigator, 'plugins', {
            value: {
                length: 0,
                item: () => null,
                namedItem: () => null,
                refresh: () => {}
            },
            writable: false
        });

        Object.defineProperty(navigator, 'mimeTypes', {
            value: {
                length: 0,
                item: () => null,
                namedItem: () => null
            },
            writable: false
        });
    }

    // Hardware concurrency protection
    function shimHardware() {
        Object.defineProperty(navigator, 'hardwareConcurrency', {
            value: 4, // Report a common core count
            writable: false
        });

        Object.defineProperty(navigator, 'deviceMemory', {
            value: 8, // Report common RAM amount in GB
            writable: false
        });
    }

    // Connection API blocking
    function shimConnection() {
        if ('connection' in navigator) {
            Object.defineProperty(navigator, 'connection', {
                value: undefined,
                writable: false
            });
        }
    }

    // GamePad API blocking
    function shimGamepad() {
        navigator.getGamepads = function() {
            return [];
        };
    }

    // Permissions API limiting
    function shimPermissions() {
        if (navigator.permissions && navigator.permissions.query) {
            const originalQuery = navigator.permissions.query;
            navigator.permissions.query = function(permissionDesc) {
                // Always return 'prompt' to prevent fingerprinting
                return Promise.resolve({ state: 'prompt' });
            };
        }
    }

    // Initialize shims
    function initializeShims() {
        try {
            applyShimConfig();
            
            shimCanvas();
            shimAudio();
            shimWebGL();
            shimTiming();
            shimBattery();
            shimScreen();
            shimTimezone();
            shimFonts();
            shimLanguages();
            shimPlugins();
            shimHardware();
            shimConnection();
            shimGamepad();
            shimPermissions();

            console.log('EphemeralBrowser: Anti-fingerprinting shims initialized for', window.location.hostname);
        } catch (error) {
            console.error('EphemeralBrowser: Failed to initialize shims:', error);
        }
    }

    // Expose configuration interface for user control
    window.EphemeralBrowserShims = {
        config: SHIM_CONFIG,
        updateConfig: function(newConfig) {
            Object.assign(SHIM_CONFIG, newConfig);
            console.log('EphemeralBrowser: Shim configuration updated');
        },
        isEnabled: function(shimName) {
            return SHIM_CONFIG[shimName] && SHIM_CONFIG[shimName].enabled;
        },
        disable: function(shimName) {
            if (SHIM_CONFIG[shimName]) {
                SHIM_CONFIG[shimName].enabled = false;
                console.log(`EphemeralBrowser: ${shimName} shim disabled`);
            }
        },
        enable: function(shimName) {
            if (SHIM_CONFIG[shimName]) {
                SHIM_CONFIG[shimName].enabled = true;
                console.log(`EphemeralBrowser: ${shimName} shim enabled`);
            }
        }
    };

    // Initialize immediately
    initializeShims();

    // Also initialize on DOM ready for late-loaded scripts
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initializeShims);
    }

})();