# EphemeralBrowser Performance Guide

## Expected Load Times 🚀

### Normal Load Times by Site Type:
- **Simple sites** (example.com): 200-500ms
- **News sites** (BBC, CNN): 800-1500ms  
- **Social media** (YouTube, Facebook): 1500-3000ms
- **Complex web apps** (Gmail, Office 365): 2000-4000ms

### EphemeralBrowser Overhead:
- **Container initialization**: ~50-100ms
- **URL sanitization**: 0-15ms per navigation
- **Anti-fingerprinting shims**: 10-50ms injection time
- **Total browser overhead**: Usually <150ms

## Performance Optimization Tips 💡

### For Faster Browsing:
1. **Use Minimal Privacy Level** for performance-critical sites
2. **Disable unnecessary shims** (Canvas, WebGL) for better performance
3. **Clear containers periodically** to free memory
4. **Close unused tabs** to reduce memory pressure

### Understanding High Load Times:
- **2000ms+ is normal** for heavy sites like YouTube with lots of JavaScript
- **Most delay comes from the website**, not EphemeralBrowser
- **Network speed** significantly impacts load times
- **Ad blockers** in other browsers often make sites appear faster

### Performance Monitoring:
- **Memory usage** updates every 10 seconds
- **Load times** measure full page load (including site's JavaScript)
- **Sanitizer overhead** shows only EphemeralBrowser processing time
- **Active shims** count enabled privacy protections

## Privacy vs Performance Trade-offs ⚖️

### Privacy Levels:
- **Strict**: Maximum protection, slight performance impact
- **Balanced**: Good protection with optimized performance (recommended)
- **Minimal**: Best performance, basic privacy protection

### Shim Performance Impact:
- **Canvas Protection**: ~5-20ms per page
- **Timing Protection**: ~1-5ms per page  
- **WebGL Protection**: ~2-10ms per page
- **Audio Protection**: ~3-15ms per page
- **Battery Blocking**: <1ms per page

## Troubleshooting Slow Sites 🔧

If a site loads slower than expected:
1. Check if URL sanitization occurred (adds one redirect)
2. Try disabling individual privacy protections to isolate impact
3. Compare with same site in regular browser
4. Check network connection speed
5. Some sites detect privacy tools and load slower intentionally

## Performance Targets (CLAUDE.md) 📊

- **Cold start**: <800ms (application launch)
- **Warm start**: <300ms (new tab creation)
- **Sanitizer overhead**: <150ms (URL processing only)
- **Memory per container**: <200MB steady-state
- **Container teardown**: <500ms (profile cleanup)

Note: Site load times are separate from these browser performance targets.
