use std::collections::VecDeque;

/// Mono f32 ring buffer connecting the input callback to the output
/// callback. Capacity-bounded: on overflow the oldest samples are dropped
/// (keeps latency from growing forever if the callbacks drift).
pub struct Ring {
    buf: VecDeque<f32>,
    capacity: usize,
    underruns: u64,
    overruns: u64,
}

impl Ring {
    pub fn new(capacity: usize) -> Self {
        Self {
            buf: VecDeque::with_capacity(capacity),
            capacity,
            underruns: 0,
            overruns: 0,
        }
    }

    /// Push mono samples; returns how many were dropped (overflow).
    pub fn push(&mut self, samples: &[f32]) -> usize {
        let mut dropped = 0;
        for &s in samples {
            if self.buf.len() >= self.capacity {
                self.buf.pop_front();
                self.overruns += 1;
                dropped += 1;
            }
            self.buf.push_back(s);
        }
        dropped
    }

    /// Pop up to `out.len()` samples; returns the number actually popped.
    /// Short reads are counted as underruns (caller pads with silence).
    pub fn pop(&mut self, out: &mut [f32]) -> usize {
        let n = out.len().min(self.buf.len());
        for slot in out.iter_mut().take(n) {
            *slot = self.buf.pop_front().unwrap_or(0.0);
        }
        if n < out.len() {
            self.underruns += 1;
        }
        n
    }

    pub fn len(&self) -> usize {
        self.buf.len()
    }

    pub fn is_empty(&self) -> bool {
        self.buf.is_empty()
    }

    pub fn underruns(&self) -> u64 {
        self.underruns
    }

    pub fn overruns(&self) -> u64 {
        self.overruns
    }
}
