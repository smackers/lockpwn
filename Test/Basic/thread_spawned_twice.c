extern void __VERIFIER_error() __attribute__ ((__noreturn__));

#include <pthread.h>

int data = 0;

void *t1(void *arg) {
  pthread_mutex_t* lock = arg;
  pthread_mutex_lock(lock);
  data++;
  pthread_mutex_unlock(lock);
  return 0;
}

int main() {
  pthread_mutex_t lock;
  pthread_mutex_init(&lock, 0);

  pthread_t tid1, tid2;

  pthread_create(&tid1, 0, t1, &lock);
  pthread_create(&tid2, 0, t1, &lock);

  pthread_join(tid1, 0);
  pthread_join(tid2, 0);

  if (data < 2) {
    ERROR: __VERIFIER_error();
  }

  return 0;
}
